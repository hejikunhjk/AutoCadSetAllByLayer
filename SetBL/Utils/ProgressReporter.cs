// filepath: SetBL/SetBL/Utils/ProgressReporter.cs
using Autodesk.AutoCAD.EditorInput;

namespace SetBL.Utils;

/// <summary>
/// 进度报告工具类
/// </summary>
public class ProgressReporter
{
    private readonly Editor _editor;
    private int _lastReportTime;
    private readonly int _reportIntervalMs;
    private int _reportIntervalObjects;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="editor">AutoCAD编辑器</param>
    /// <param name="reportIntervalMs">报告间隔（毫秒）</param>
    /// <param name="reportIntervalObjects">每N个对象报告一次</param>
    public ProgressReporter(Editor editor, int reportIntervalMs = 2000, int reportIntervalObjects = 5)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _reportIntervalMs = reportIntervalMs;
        _reportIntervalObjects = reportIntervalObjects;
        _lastReportTime = 0;
    }

    /// <summary>
    /// 报告进度（每N个对象或每指定毫秒报告一次）
    /// </summary>
    /// <param name="current">当前进度</param>
    /// <param name="total">总数</param>
    /// <param name="modifiedCount">已修改数</param>
    /// <param name="skippedCount">已跳过数</param>
    /// <param name="currentTimeMs">当前时间（毫秒）</param>
    public void ReportProgress(int current, int total, int modifiedCount, int skippedCount, int currentTimeMs)
    {
        bool shouldReport = false;

        // 每N个对象报告一次
        if (current % _reportIntervalObjects == 0)
        {
            shouldReport = true;
        }

        // 或超过报告间隔时间
        if (currentTimeMs - _lastReportTime > _reportIntervalMs)
        {
            shouldReport = true;
        }

        if (shouldReport && total > 0)
        {
            int percent = (int)((double)current / total * 100);
            _editor.WriteMessage($"\r>> 正在处理块 {current}/{total} ({percent}%)  |  已修改: {modifiedCount}  已跳过: {skippedCount} 个对象...");
            _lastReportTime = currentTimeMs;
        }
    }

    /// <summary>
    /// 强制报告当前进度
    /// </summary>
    public void ForceReport(int current, int total, int modifiedCount, int skippedCount)
    {
        if (total > 0)
        {
            int percent = (int)((double)current / total * 100);
            _editor.WriteMessage($"\r>> 正在处理块 {current}/{total} ({percent}%)  |  已修改: {modifiedCount}  已跳过: {skippedCount} 个对象...");
        }
    }

    /// <summary>
    /// 清除进度行（写空格覆盖）
    /// </summary>
    public void ClearProgress()
    {
        _editor.WriteMessage("\r" + new string(' ', 80) + "\r");
    }

    /// <summary>
    /// 重置报告计时器
    /// </summary>
    public void ResetTimer()
    {
        _lastReportTime = 0;
    }
}
