using System.IO;
using D2RDropTracker.Models;
using Microsoft.Data.Sqlite;

namespace D2RDropTracker.Data;

public sealed class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly string _backupDirectory;

    public DatabaseService(string? dataDirectoryOverride = null)
    {
        var dataDirectory = dataDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RDropTracker");
        Directory.CreateDirectory(dataDirectory);
        _databasePath = Path.Combine(dataDirectory, "tracker.db");
        _backupDirectory = Path.Combine(dataDirectory, "Backups");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            ForeignKeys = true
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Runs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Character TEXT NOT NULL,
                Area TEXT NOT NULL,
                Difficulty TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NULL,
                DurationSeconds INTEGER NULL,
                IsCompleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Drops (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                ItemName TEXT NOT NULL,
                Category TEXT NOT NULL,
                Quality TEXT NOT NULL,
                DroppedAt TEXT NOT NULL,
                FOREIGN KEY (RunId) REFERENCES Runs(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DeletedDrops (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OriginalDropId INTEGER NOT NULL,
                RunId INTEGER NOT NULL,
                ItemName TEXT NOT NULL,
                Category TEXT NOT NULL,
                Quality TEXT NOT NULL,
                DroppedAt TEXT NOT NULL,
                DeletedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Drops_RunId ON Drops(RunId);
            CREATE INDEX IF NOT EXISTS IX_Drops_ItemName ON Drops(ItemName);
            CREATE INDEX IF NOT EXISTS IX_DeletedDrops_DeletedAt ON DeletedDrops(DeletedAt);
            """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "Runs", "TimerElapsedSeconds",
            "ALTER TABLE Runs ADD COLUMN TimerElapsedSeconds INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(connection, "Runs", "TimerResumedAt",
            "ALTER TABLE Runs ADD COLUMN TimerResumedAt TEXT NULL;");
        EnsureColumn(connection, "Runs", "IsTimerRunning",
            "ALTER TABLE Runs ADD COLUMN IsTimerRunning INTEGER NOT NULL DEFAULT 1;");
        EnsureColumn(connection, "Runs", "PlayerCount",
            "ALTER TABLE Runs ADD COLUMN PlayerCount INTEGER NOT NULL DEFAULT 1;");
        EnsureColumn(connection, "Runs", "MagicFind",
            "ALTER TABLE Runs ADD COLUMN MagicFind INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(connection, "Runs", "Tags",
            "ALTER TABLE Runs ADD COLUMN Tags TEXT NOT NULL DEFAULT '';");
        EnsureColumn(connection, "Runs", "Notes",
            "ALTER TABLE Runs ADD COLUMN Notes TEXT NOT NULL DEFAULT '';");
    }

    public string CreateDailyBackup()
    {
        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(_backupDirectory, $"tracker-{DateTime.Today:yyyy-MM-dd}.db");
        if (File.Exists(backupPath))
        {
            return backupPath;
        }

        using var source = OpenConnection();
        using var destination = new SqliteConnection($"Data Source={backupPath}");
        destination.Open();
        source.BackupDatabase(destination);
        return backupPath;
    }

    public int CleanupOldBackups(int retentionDays)
    {
        Directory.CreateDirectory(_backupDirectory);
        var cutoff = DateTime.Now.AddDays(-Math.Max(1, retentionDays));
        var deleted = 0;
        foreach (var file in Directory.GetFiles(_backupDirectory, "*.db"))
        {
            if (File.GetLastWriteTime(file) >= cutoff)
            {
                continue;
            }
            File.Delete(file);
            deleted++;
        }
        return deleted;
    }

    public string CheckIntegrity()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return command.ExecuteScalar()?.ToString() ?? "unknown";
    }

    public string CreateManualBackup()
    {
        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(
            _backupDirectory,
            $"tracker-manual-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        using var source = OpenConnection();
        using var destination = new SqliteConnection($"Data Source={backupPath}");
        destination.Open();
        source.BackupDatabase(destination);
        return backupPath;
    }

    public List<BackupInfo> GetBackups()
    {
        Directory.CreateDirectory(_backupDirectory);
        return Directory.GetFiles(_backupDirectory, "*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .Select(file => new BackupInfo
            {
                FilePath = file.FullName,
                CreatedAt = file.LastWriteTime,
                SizeBytes = file.Length
            })
            .ToList();
    }

    public string GetBackupDirectory() => _backupDirectory;

    public void RestoreBackup(string backupPath)
    {
        ValidateDatabase(backupPath);
        Directory.CreateDirectory(_backupDirectory);
        if (File.Exists(_databasePath))
        {
            var safetyPath = Path.Combine(
                _backupDirectory,
                $"tracker-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            File.Copy(_databasePath, safetyPath, true);
        }
        File.Copy(backupPath, _databasePath, true);
        Initialize();
    }

    public RunSession GetOrCreateActiveRun(string character, string area, string difficulty)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Character, Area, Difficulty, StartedAt, EndedAt, DurationSeconds,
                   IsCompleted, TimerElapsedSeconds, TimerResumedAt, IsTimerRunning,
                   PlayerCount, MagicFind, Tags, Notes
            FROM Runs
            WHERE IsCompleted = 0
            ORDER BY Id DESC
            LIMIT 1;
            """;
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? ReadRun(reader)
            : CreateRun(character, area, difficulty, DateTime.Now);
    }

    public RunSession CreateRun(string character, string area, string difficulty, DateTime startedAt)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Runs (
                Character, Area, Difficulty, StartedAt, IsCompleted,
                TimerElapsedSeconds, TimerResumedAt, IsTimerRunning)
            VALUES ($character, $area, $difficulty, $startedAt, 0, 0, $startedAt, 1);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$character", Normalize(character, "未命名角色"));
        command.Parameters.AddWithValue("$area", Normalize(area, "其他"));
        command.Parameters.AddWithValue("$difficulty", Normalize(difficulty, "地狱"));
        command.Parameters.AddWithValue("$startedAt", startedAt.ToString("O"));
        var id = (long)(command.ExecuteScalar() ?? 0L);
        return new RunSession
        {
            Id = id,
            Character = Normalize(character, "未命名角色"),
            Area = Normalize(area, "其他"),
            Difficulty = Normalize(difficulty, "地狱"),
            StartedAt = startedAt,
            TimerElapsedSeconds = 0,
            TimerResumedAt = startedAt,
            IsTimerRunning = true
        };
    }

    public void CompleteRun(long runId, DateTime endedAt, TimeSpan duration,
        string character, string area, string difficulty, int playerCount = 1,
        int magicFind = 0, string tags = "", string notes = "")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Runs
            SET Character = $character,
                Area = $area,
                Difficulty = $difficulty,
                EndedAt = $endedAt,
                DurationSeconds = $durationSeconds,
                IsCompleted = 1,
                TimerElapsedSeconds = $durationSeconds,
                TimerResumedAt = NULL,
                IsTimerRunning = 0,
                PlayerCount = $playerCount,
                MagicFind = $magicFind,
                Tags = $tags,
                Notes = $notes
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$character", Normalize(character, "未命名角色"));
        command.Parameters.AddWithValue("$area", Normalize(area, "其他"));
        command.Parameters.AddWithValue("$difficulty", Normalize(difficulty, "地狱"));
        command.Parameters.AddWithValue("$playerCount", Math.Clamp(playerCount, 1, 8));
        command.Parameters.AddWithValue("$magicFind", Math.Max(0, magicFind));
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.Parameters.AddWithValue("$notes", notes.Trim());
        command.Parameters.AddWithValue("$endedAt", endedAt.ToString("O"));
        command.Parameters.AddWithValue("$durationSeconds", Math.Max(0, (long)duration.TotalSeconds));
        command.Parameters.AddWithValue("$id", runId);
        command.ExecuteNonQuery();
    }

    public void UpdateTimerState(long runId, int elapsedSeconds, DateTime? resumedAt, bool isRunning)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Runs
            SET TimerElapsedSeconds = $elapsedSeconds,
                TimerResumedAt = $resumedAt,
                IsTimerRunning = $isRunning
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$elapsedSeconds", Math.Max(0, elapsedSeconds));
        command.Parameters.AddWithValue("$resumedAt",
            resumedAt is null ? DBNull.Value : resumedAt.Value.ToString("O"));
        command.Parameters.AddWithValue("$isRunning", isRunning ? 1 : 0);
        command.Parameters.AddWithValue("$id", runId);
        command.ExecuteNonQuery();
    }

    public void ResetRunStart(long runId, DateTime startedAt, bool isRunning)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Runs
            SET StartedAt = $startedAt,
                TimerElapsedSeconds = 0,
                TimerResumedAt = $resumedAt,
                IsTimerRunning = $isRunning
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$startedAt", startedAt.ToString("O"));
        command.Parameters.AddWithValue("$resumedAt",
            isRunning ? startedAt.ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue("$isRunning", isRunning ? 1 : 0);
        command.Parameters.AddWithValue("$id", runId);
        command.ExecuteNonQuery();
    }

    public void AddDrop(long runId, string itemName, string category, string quality, DateTime droppedAt)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Drops (RunId, ItemName, Category, Quality, DroppedAt)
            VALUES ($runId, $itemName, $category, $quality, $droppedAt);
            """;
        command.Parameters.AddWithValue("$runId", runId);
        command.Parameters.AddWithValue("$itemName", itemName);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$quality", quality);
        command.Parameters.AddWithValue("$droppedAt", droppedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void DeleteDrop(long dropId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var archive = connection.CreateCommand();
        archive.Transaction = transaction;
        archive.CommandText =
            """
            INSERT INTO DeletedDrops (
                OriginalDropId, RunId, ItemName, Category, Quality, DroppedAt, DeletedAt)
            SELECT Id, RunId, ItemName, Category, Quality, DroppedAt, $deletedAt
            FROM Drops WHERE Id = $id;
            """;
        archive.Parameters.AddWithValue("$deletedAt", DateTime.Now.ToString("O"));
        archive.Parameters.AddWithValue("$id", dropId);
        archive.ExecuteNonQuery();

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM Drops WHERE Id = $id;";
        delete.Parameters.AddWithValue("$id", dropId);
        delete.ExecuteNonQuery();
        transaction.Commit();
    }

    public void RestoreDrop(DropRecord record)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Drops (RunId, ItemName, Category, Quality, DroppedAt)
            VALUES ($runId, $itemName, $category, $quality, $droppedAt);
            """;
        command.Parameters.AddWithValue("$runId", record.RunId);
        command.Parameters.AddWithValue("$itemName", record.ItemName);
        command.Parameters.AddWithValue("$category", record.Category);
        command.Parameters.AddWithValue("$quality", record.Quality);
        command.Parameters.AddWithValue("$droppedAt", record.DroppedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public List<DeletedDropRecord> GetDeletedDrops()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT dd.Id, dd.OriginalDropId, dd.RunId, dd.ItemName, dd.Category,
                   dd.Quality, dd.DroppedAt, dd.DeletedAt,
                   COALESCE((SELECT COUNT(*) FROM Runs numbered
                             WHERE numbered.Id <= dd.RunId), 0) AS RunNumber
            FROM DeletedDrops dd
            ORDER BY dd.Id DESC;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<DeletedDropRecord>();
        while (reader.Read())
        {
            result.Add(new DeletedDropRecord
            {
                Id = reader.GetInt64(0),
                OriginalDropId = reader.GetInt64(1),
                RunId = reader.GetInt64(2),
                ItemName = reader.GetString(3),
                Category = reader.GetString(4),
                Quality = reader.GetString(5),
                DroppedAt = DateTime.Parse(reader.GetString(6)),
                DeletedAt = DateTime.Parse(reader.GetString(7)),
                RunNumber = reader.GetInt32(8)
            });
        }
        return result;
    }

    public bool RestoreDeletedDrop(long deletedDropId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var restore = connection.CreateCommand();
        restore.Transaction = transaction;
        restore.CommandText =
            """
            INSERT INTO Drops (RunId, ItemName, Category, Quality, DroppedAt)
            SELECT RunId, ItemName, Category, Quality, DroppedAt
            FROM DeletedDrops
            WHERE Id = $id
              AND EXISTS (SELECT 1 FROM Runs WHERE Runs.Id = DeletedDrops.RunId);
            """;
        restore.Parameters.AddWithValue("$id", deletedDropId);
        var inserted = restore.ExecuteNonQuery();
        if (inserted == 0)
        {
            transaction.Rollback();
            return false;
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM DeletedDrops WHERE Id = $id;";
        delete.Parameters.AddWithValue("$id", deletedDropId);
        delete.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }

    public void PermanentlyDeleteDeletedDrop(long deletedDropId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DeletedDrops WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", deletedDropId);
        command.ExecuteNonQuery();
    }

    public void EmptyDeletedDrops()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DeletedDrops;";
        command.ExecuteNonQuery();
    }

    public void UpdateDrop(long dropId, string itemName, string category, string quality)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Drops
            SET ItemName = $itemName, Category = $category, Quality = $quality
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$itemName", Normalize(itemName, "未命名物品"));
        command.Parameters.AddWithValue("$category", Normalize(category, "其他"));
        command.Parameters.AddWithValue("$quality", Normalize(quality, "未知"));
        command.Parameters.AddWithValue("$id", dropId);
        command.ExecuteNonQuery();
    }

    public void UpdateDrop(
        long dropId, long runId, string itemName, string category, string quality)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Drops
            SET RunId = $runId, ItemName = $itemName, Category = $category, Quality = $quality
            WHERE Id = $id AND EXISTS (SELECT 1 FROM Runs WHERE Id = $runId);
            """;
        command.Parameters.AddWithValue("$runId", runId);
        command.Parameters.AddWithValue("$itemName", Normalize(itemName, "未命名物品"));
        command.Parameters.AddWithValue("$category", Normalize(category, "其他"));
        command.Parameters.AddWithValue("$quality", Normalize(quality, "未知"));
        command.Parameters.AddWithValue("$id", dropId);
        command.ExecuteNonQuery();
    }

    public bool TryUndoLastCompletedRun(long currentActiveRunId, out RunSession? restoredRun)
    {
        restoredRun = null;
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM Drops WHERE RunId = $runId;";
        countCommand.Parameters.AddWithValue("$runId", currentActiveRunId);
        if (Convert.ToInt32(countCommand.ExecuteScalar()) > 0)
        {
            transaction.Rollback();
            return false;
        }

        using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText =
            """
            SELECT Id, COALESCE(DurationSeconds, 0)
            FROM Runs WHERE IsCompleted = 1 ORDER BY Id DESC LIMIT 1;
            """;
        using var found = findCommand.ExecuteReader();
        if (!found.Read())
        {
            transaction.Rollback();
            return false;
        }
        var previousRunId = found.GetInt64(0);
        var previousDuration = found.GetInt32(1);
        found.Close();

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM Runs WHERE Id = $id AND IsCompleted = 0;";
        deleteCommand.Parameters.AddWithValue("$id", currentActiveRunId);
        deleteCommand.ExecuteNonQuery();

        using var restoreCommand = connection.CreateCommand();
        restoreCommand.Transaction = transaction;
        restoreCommand.CommandText =
            """
            UPDATE Runs
            SET EndedAt = NULL, DurationSeconds = NULL, IsCompleted = 0,
                TimerElapsedSeconds = $elapsed, TimerResumedAt = $resumedAt, IsTimerRunning = 1
            WHERE Id = $id;
            """;
        restoreCommand.Parameters.AddWithValue("$elapsed", previousDuration);
        restoreCommand.Parameters.AddWithValue("$resumedAt", DateTime.Now.ToString("O"));
        restoreCommand.Parameters.AddWithValue("$id", previousRunId);
        restoreCommand.ExecuteNonQuery();

        using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText =
            """
            SELECT Id, Character, Area, Difficulty, StartedAt, EndedAt, DurationSeconds,
                   IsCompleted, TimerElapsedSeconds, TimerResumedAt, IsTimerRunning,
                   PlayerCount, MagicFind, Tags, Notes
            FROM Runs WHERE Id = $id;
            """;
        readCommand.Parameters.AddWithValue("$id", previousRunId);
        using var reader = readCommand.ExecuteReader();
        if (reader.Read())
        {
            restoredRun = ReadRun(reader);
        }

        transaction.Commit();
        return restoredRun is not null;
    }

    public int GetCompletedRunCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Runs WHERE IsCompleted = 1;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public DashboardSummary GetSummary()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM Runs WHERE IsCompleted = 1),
                (SELECT COUNT(*) FROM Drops),
                COALESCE((SELECT AVG(DurationSeconds) FROM Runs WHERE IsCompleted = 1), 0);
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        return new DashboardSummary
        {
            TotalRuns = reader.GetInt32(0),
            TotalDrops = reader.GetInt32(1),
            AverageSeconds = reader.GetDouble(2)
        };
    }

    public List<RunHistoryItem> GetRunHistory(RunFilter filter)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var conditions = new List<string> { "r.IsCompleted = 1" };

        if (filter.StartDate is not null)
        {
            conditions.Add("r.StartedAt >= $startDate");
            command.Parameters.AddWithValue("$startDate", filter.StartDate.Value.Date.ToString("O"));
        }
        if (filter.EndDate is not null)
        {
            conditions.Add("r.StartedAt < $endDate");
            command.Parameters.AddWithValue("$endDate", filter.EndDate.Value.Date.AddDays(1).ToString("O"));
        }
        AddTextFilter(command, conditions, "r.Character", "$character", filter.Character);
        AddTextFilter(command, conditions, "r.Area", "$area", filter.Area);
        AddTextFilter(command, conditions, "r.Difficulty", "$difficulty", filter.Difficulty);
        AddTextFilter(command, conditions, "r.Tags", "$tags", filter.Tags);

        command.CommandText =
            $"""
            SELECT r.Id,
                   (SELECT COUNT(*) FROM Runs numbered
                    WHERE numbered.IsCompleted = 1 AND numbered.Id <= r.Id) AS RunNumber,
                   r.Character, r.Area, r.Difficulty, r.StartedAt, r.EndedAt,
                   COALESCE(r.DurationSeconds, 0),
                   (SELECT COUNT(*) FROM Drops d WHERE d.RunId = r.Id) AS DropCount,
                   r.PlayerCount, r.MagicFind, r.Tags, r.Notes
            FROM Runs r
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY r.Id DESC;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<RunHistoryItem>();
        while (reader.Read())
        {
            result.Add(new RunHistoryItem
            {
                Id = reader.GetInt64(0),
                RunNumber = reader.GetInt32(1),
                Character = reader.GetString(2),
                Area = reader.GetString(3),
                Difficulty = reader.GetString(4),
                StartedAt = DateTime.Parse(reader.GetString(5)),
                EndedAt = reader.IsDBNull(6)
                    ? DateTime.Parse(reader.GetString(5))
                    : DateTime.Parse(reader.GetString(6)),
                DurationSeconds = reader.GetInt32(7),
                DropCount = reader.GetInt32(8),
                PlayerCount = reader.GetInt32(9),
                MagicFind = reader.GetInt32(10),
                Tags = reader.GetString(11),
                Notes = reader.GetString(12)
            });
        }
        return result;
    }

    public List<RunChoice> GetRunChoices()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id,
                   (SELECT COUNT(*) FROM Runs numbered WHERE numbered.Id <= r.Id) AS RunNumber,
                   Area, StartedAt, IsCompleted
            FROM Runs r
            ORDER BY Id DESC;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<RunChoice>();
        while (reader.Read())
        {
            var status = reader.GetInt32(4) == 1 ? "已完成" : "进行中";
            result.Add(new RunChoice
            {
                Id = reader.GetInt64(0),
                DisplayName =
                    $"第 {reader.GetInt32(1)} 场 | {reader.GetString(2)} | " +
                    $"{DateTime.Parse(reader.GetString(3)):MM-dd HH:mm} | {status}"
            });
        }
        return result;
    }

    public void UpdateCompletedRun(
        long runId,
        string character,
        string area,
        string difficulty,
        DateTime startedAt,
        DateTime endedAt,
        int playerCount = 1,
        int magicFind = 0,
        string tags = "",
        string notes = "")
    {
        if (endedAt < startedAt)
        {
            throw new ArgumentException("结束时间不能早于开始时间。");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Runs
            SET Character = $character,
                Area = $area,
                Difficulty = $difficulty,
                StartedAt = $startedAt,
                EndedAt = $endedAt,
                DurationSeconds = $durationSeconds,
                TimerElapsedSeconds = $durationSeconds,
                PlayerCount = $playerCount,
                MagicFind = $magicFind,
                Tags = $tags,
                Notes = $notes
            WHERE Id = $id AND IsCompleted = 1;
            """;
        command.Parameters.AddWithValue("$character", Normalize(character, "未命名角色"));
        command.Parameters.AddWithValue("$area", Normalize(area, "其他"));
        command.Parameters.AddWithValue("$difficulty", Normalize(difficulty, "地狱"));
        command.Parameters.AddWithValue("$startedAt", startedAt.ToString("O"));
        command.Parameters.AddWithValue("$endedAt", endedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$durationSeconds", Math.Max(0, (long)(endedAt - startedAt).TotalSeconds));
        command.Parameters.AddWithValue("$playerCount", Math.Clamp(playerCount, 1, 8));
        command.Parameters.AddWithValue("$magicFind", Math.Max(0, magicFind));
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.Parameters.AddWithValue("$notes", notes.Trim());
        command.Parameters.AddWithValue("$id", runId);
        command.ExecuteNonQuery();
    }

    public List<DropRecord> GetFilteredDrops(RunFilter filter)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var conditions = new List<string> { "r.IsCompleted = 1" };
        ApplyRunFilter(command, conditions, filter);
        command.CommandText =
            $"""
            SELECT d.Id, d.RunId, d.ItemName, d.Category, d.Quality, d.DroppedAt,
                   r.Character, r.Area, r.Difficulty,
                   (SELECT COUNT(*) FROM Runs numbered WHERE numbered.Id <= r.Id) AS RunNumber,
                   r.StartedAt, r.EndedAt
            FROM Drops d
            JOIN Runs r ON r.Id = d.RunId
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY d.Id DESC;
            """;
        return ReadDrops(command);
    }

    public List<DropStatistic> GetFilteredDropStatistics(RunFilter filter, int completedRuns)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var conditions = new List<string> { "r.IsCompleted = 1" };
        ApplyRunFilter(command, conditions, filter);
        command.CommandText =
            $"""
            SELECT d.ItemName, d.Category, COUNT(*) AS ItemCount
            FROM Drops d
            JOIN Runs r ON r.Id = d.RunId
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY d.ItemName, d.Category
            ORDER BY ItemCount DESC, d.ItemName;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<DropStatistic>();
        while (reader.Read())
        {
            var count = reader.GetInt32(2);
            result.Add(new DropStatistic
            {
                ItemName = reader.GetString(0),
                Category = reader.GetString(1),
                Count = count,
                PerHundredRuns = completedRuns == 0 ? 0 : count * 100.0 / completedRuns
            });
        }
        return result;
    }

    public ChartData GetChartData(RunFilter filter)
    {
        var runs = GetRunHistory(filter);
        var drops = GetFilteredDrops(filter);
        return new ChartData
        {
            DailyRuns = runs
                .GroupBy(run => run.StartedAt.Date)
                .OrderBy(group => group.Key)
                .TakeLast(14)
                .Select(group => new ChartPoint
                {
                    Label = group.Key.ToString("MM-dd"),
                    Value = group.Count()
                }).ToList(),
            AreaAverageSeconds = runs
                .GroupBy(run => run.Area)
                .OrderByDescending(group => group.Count())
                .Take(8)
                .Select(group => new ChartPoint
                {
                    Label = group.Key,
                    Value = group.Average(run => run.DurationSeconds)
                }).ToList(),
            CategoryDrops = drops
                .GroupBy(drop => drop.Category)
                .OrderByDescending(group => group.Count())
                .Select(group => new ChartPoint
                {
                    Label = group.Key,
                    Value = group.Count()
                }).ToList()
        };
    }

    public bool DeleteCompletedRun(long runId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Runs WHERE Id = $id AND IsCompleted = 1;";
        command.Parameters.AddWithValue("$id", runId);
        return command.ExecuteNonQuery() > 0;
    }

    public List<DropRecord> GetRecentDrops(int limit)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT d.Id, d.RunId, d.ItemName, d.Category, d.Quality, d.DroppedAt,
                   r.Character, r.Area, r.Difficulty,
                   (SELECT COUNT(*) FROM Runs numbered WHERE numbered.Id <= r.Id) AS RunNumber,
                   r.StartedAt, r.EndedAt
            FROM Drops d
            JOIN Runs r ON r.Id = d.RunId
            ORDER BY d.Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        return ReadDrops(command);
    }

    public List<DropRecord> GetAllDrops()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT d.Id, d.RunId, d.ItemName, d.Category, d.Quality, d.DroppedAt,
                   r.Character, r.Area, r.Difficulty,
                   (SELECT COUNT(*) FROM Runs numbered WHERE numbered.Id <= r.Id) AS RunNumber,
                   r.StartedAt, r.EndedAt
            FROM Drops d
            JOIN Runs r ON r.Id = d.RunId
            ORDER BY d.Id;
            """;
        return ReadDrops(command);
    }

    public List<DropStatistic> GetDropStatistics(int completedRuns)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ItemName, Category, COUNT(*) AS ItemCount
            FROM Drops
            GROUP BY ItemName, Category
            ORDER BY ItemCount DESC, ItemName;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<DropStatistic>();
        while (reader.Read())
        {
            var count = reader.GetInt32(2);
            result.Add(new DropStatistic
            {
                ItemName = reader.GetString(0),
                Category = reader.GetString(1),
                Count = count,
                PerHundredRuns = completedRuns == 0 ? 0 : count * 100.0 / completedRuns
            });
        }
        return result;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static List<DropRecord> ReadDrops(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var result = new List<DropRecord>();
        while (reader.Read())
        {
            result.Add(new DropRecord
            {
                Id = reader.GetInt64(0),
                RunId = reader.GetInt64(1),
                ItemName = reader.GetString(2),
                Category = reader.GetString(3),
                Quality = reader.GetString(4),
                DroppedAt = DateTime.Parse(reader.GetString(5)),
                Character = reader.GetString(6),
                Area = reader.GetString(7),
                Difficulty = reader.GetString(8),
                RunNumber = reader.GetInt32(9),
                RunStartedAt = DateTime.Parse(reader.GetString(10)),
                RunEndedAt = reader.IsDBNull(11) ? null : DateTime.Parse(reader.GetString(11))
            });
        }
        return result;
    }

    private static RunSession ReadRun(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Character = reader.GetString(1),
        Area = reader.GetString(2),
        Difficulty = reader.GetString(3),
        StartedAt = DateTime.Parse(reader.GetString(4)),
        EndedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
        DurationSeconds = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        IsCompleted = reader.GetInt32(7) == 1,
        TimerElapsedSeconds = reader.GetInt32(8),
        TimerResumedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
        IsTimerRunning = reader.GetInt32(10) == 1,
        PlayerCount = reader.GetInt32(11),
        MagicFind = reader.GetInt32(12),
        Tags = reader.GetString(13),
        Notes = reader.GetString(14)
    };

    private static void EnsureColumn(
        SqliteConnection connection, string table, string column, string alterSql)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        alter.ExecuteNonQuery();
    }

    private static void AddTextFilter(
        SqliteCommand command, List<string> conditions, string column, string parameter, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        conditions.Add($"{column} LIKE {parameter}");
        command.Parameters.AddWithValue(parameter, $"%{value.Trim()}%");
    }

    private static void ApplyRunFilter(
        SqliteCommand command, List<string> conditions, RunFilter filter)
    {
        if (filter.StartDate is not null)
        {
            conditions.Add("r.StartedAt >= $filterStartDate");
            command.Parameters.AddWithValue(
                "$filterStartDate", filter.StartDate.Value.Date.ToString("O"));
        }
        if (filter.EndDate is not null)
        {
            conditions.Add("r.StartedAt < $filterEndDate");
            command.Parameters.AddWithValue(
                "$filterEndDate", filter.EndDate.Value.Date.AddDays(1).ToString("O"));
        }
        AddTextFilter(command, conditions, "r.Character", "$filterCharacter", filter.Character);
        AddTextFilter(command, conditions, "r.Area", "$filterArea", filter.Area);
        AddTextFilter(command, conditions, "r.Difficulty", "$filterDifficulty", filter.Difficulty);
        AddTextFilter(command, conditions, "r.Tags", "$filterTags", filter.Tags);
    }

    private static void ValidateDatabase(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("备份文件不存在。", path);
        }
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = command.ExecuteScalar()?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"备份数据库校验失败：{result}");
        }
    }

    private static string Normalize(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
