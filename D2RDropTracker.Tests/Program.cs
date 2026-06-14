using D2RDropTracker.Data;
using D2RDropTracker.Models;

var testDirectory = Path.Combine(Path.GetTempPath(), $"D2RDropTrackerTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(testDirectory);

try
{
    var database = new DatabaseService(testDirectory);
    database.Initialize();

    var startedAt = new DateTime(2026, 6, 13, 10, 0, 0);
    var run = database.CreateRun("测试角色", "混沌避难所", "地狱", startedAt);
    database.AddDrop(run.Id, "符文 30 Ber", "符文", "符文", startedAt.AddMinutes(1));
    database.CompleteRun(
        run.Id,
        startedAt.AddMinutes(2),
        TimeSpan.FromSeconds(100),
        "测试角色",
        "混沌避难所",
        "地狱",
        8,
        350,
        "恐怖区域,测试",
        "自动化测试");

    var history = database.GetRunHistory(new RunFilter { Tags = "测试" });
    Assert(history.Count == 1, "标签筛选应返回一个场次");
    Assert(history[0].PlayerCount == 8, "玩家人数应保存");
    Assert(history[0].MagicFind == 350, "MF 应保存");
    Assert(history[0].DropCount == 1, "掉落计数应为 1");

    var drop = database.GetAllDrops().Single();
    database.DeleteDrop(drop.Id);
    Assert(database.GetAllDrops().Count == 0, "删除后掉落列表应为空");
    var deleted = database.GetDeletedDrops().Single();
    Assert(database.RestoreDeletedDrop(deleted.Id), "回收站记录应可恢复");
    Assert(database.GetAllDrops().Count == 1, "恢复后掉落应存在");

    database.UpdateCompletedRun(
        run.Id,
        "测试角色2",
        "崔凡克",
        "地狱",
        startedAt,
        startedAt.AddMinutes(3),
        3,
        200,
        "效率",
        "已编辑");
    history = database.GetRunHistory(new RunFilter());
    Assert(history[0].Area == "崔凡克", "场次区域应可编辑");
    Assert(history[0].DurationSeconds == 180, "编辑后耗时应重新计算");

    var chart = database.GetChartData(new RunFilter());
    Assert(chart.DailyRuns.Count == 1, "每日场次图表应有数据");
    Assert(chart.AreaAverageSeconds.Single().Value == 180, "区域平均耗时应正确");
    Assert(chart.CategoryDrops.Single().Value == 1, "分类掉落图表应正确");

    Console.WriteLine("All tests passed.");
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    Directory.Delete(testDirectory, true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
