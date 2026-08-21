// filepath: SetBL/SetBL/Commands.cs
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using SetBL.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SetBL;

/// <summary>
/// SetBL 命令类
/// 功能：遍历图纸中所有块定义，仅将颜色非ByLayer的对象设为ByLayer
/// </summary>
public class SetBLCommands
{
    // 统计变量
    private static int s_modifiedCount = 0;       // 实际修改的对象数
    private static int s_skippedCount = 0;        // 跳过的对象数（已是ByLayer）
    private static int s_blockModifiedCount = 0;  // 块内修改数
    private static int s_blockSkippedCount = 0;   // 块内跳过数

    /// <summary>
    /// SetBL 命令 - 主入口
    /// 遍历图纸中所有对象，仅将颜色非ByLayer的对象设为ByLayer
    /// </summary>
    [CommandMethod("SetBL")]
    public void SetAllByColor()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        Database db = doc.Database;
        Editor ed = doc.Editor;

        // 重置计数器
        ResetCounters();

        // 错误处理
        try
        {
            // 显示启动提示
            ed.WriteMessage("\n正在遍历图纸中所有对象（仅修改非ByLayer颜色的对象）...\n");
            ed.WriteMessage("提示：处理过程中会显示进度信息，请耐心等待。\n");

            Stopwatch sw = Stopwatch.StartNew();

            // --- 第1步：处理模型空间和图纸空间中的独立对象 ---
            ed.WriteMessage("\n>> 第1步：处理图纸空间中的独立对象...\n");
            ProcessIndependentObjects(db, ed);

            // --- 第2步：遍历所有块定义，修改内部内容颜色 ---
            ed.WriteMessage("\n>> 第2步：遍历所有块定义，修改内部内容颜色...\n");
            ProcessAllBlockDefinitions(db, ed);

            // --- 第3步：重生成 ---
            ed.WriteMessage("\n>> 第3步：重生成图形...");
            ed.Regen();
            ed.WriteMessage(" 完成\n");

            sw.Stop();

            // 输出详细结果
            OutputResults(ed, sw.ElapsedMilliseconds);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\n错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 重置所有计数器
    /// </summary>
    private static void ResetCounters()
    {
        s_modifiedCount = 0;
        s_skippedCount = 0;
        s_blockModifiedCount = 0;
        s_blockSkippedCount = 0;
    }

    /// <summary>
    /// 第1步：处理模型空间和图纸空间中的独立对象
    /// 使用 Transaction 遍历
    /// </summary>
    private void ProcessIndependentObjects(Database db, Editor ed)
    {
        int independentModified = 0;
        int independentSkipped = 0;

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            // 获取块表
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            // 遍历所有布局（模型空间和图纸空间）
            foreach (ObjectId blockId in bt)
            {
                BlockTableRecord blockRec = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                // 只处理布局（模型空间和图纸空间）
                if (!blockRec.IsLayout)
                    continue;

                // 遍历布局内的所有对象
                foreach (ObjectId entityId in blockRec)
                {
                    Entity entity = (Entity)tr.GetObject(entityId, OpenMode.ForRead);
                    if (entity != null)
                    {
                        // 检查图层是否锁定
                        try
                        {
                            LayerTableRecord layerRec = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                            if (layerRec.IsLocked)
                            {
                                independentSkipped++;
                                continue;
                            }
                        }
                        catch
                        {
                            // 无法检查图层状态，继续尝试修改
                        }

                        // 以写模式打开并修改实体（包括块引用和普通对象）
                        Entity entityToModify = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);
                        if (ColorHelper.SetToByLayer(entityToModify, tr))
                        {
                            independentModified++;
                        }
                        else
                        {
                            independentSkipped++;
                        }
                    }
                }
            }

            tr.Commit();
        }

        // 更新全局计数
        s_modifiedCount += independentModified;
        s_skippedCount += independentSkipped;

        ed.WriteMessage($"  已完成：修改 {independentModified} 个，跳过 {independentSkipped} 个（已是ByLayer或图层锁定）。\n");
    }

    /// <summary>
    /// 第2步：遍历所有块定义，修改内部内容颜色
    /// </summary>
    private void ProcessAllBlockDefinitions(Database db, Editor ed)
    {
        List<string> blockNames = new List<string>();

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            // 获取块表
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            // 收集所有块定义名称（排除模型空间和图纸空间）
            foreach (ObjectId blockId in bt)
            {
                BlockTableRecord blockRec = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                // 排除布局（模型空间和图纸空间）
                if (blockRec.IsLayout)
                    continue;

                // 排除匿名块
                if (blockRec.IsAnonymous)
                    continue;

                string blockName = blockRec.Name;
                if (!string.IsNullOrEmpty(blockName))
                {
                    blockNames.Add(blockName);
                }
            }

            tr.Commit();
        }

        int totalBlocks = blockNames.Count;
        ed.WriteMessage($"  共发现 {totalBlocks} 个块定义。\n");

        if (totalBlocks == 0)
        {
            ed.WriteMessage("  没有需要处理的块定义。\n");
            return;
        }

        // 进度报告器
        ProgressReporter reporter = new ProgressReporter(ed);
        Stopwatch blockTimer = Stopwatch.StartNew();

        // 遍历每个块定义
        int currentBlock = 0;
        foreach (string blockName in blockNames)
        {
            currentBlock++;
            ProcessSingleBlockDefinition(db, blockName);

            // 每5个块或每2秒报告一次进度
            int currentTimeMs = (int)blockTimer.ElapsedMilliseconds;
            reporter.ReportProgress(currentBlock, totalBlocks, s_modifiedCount, s_skippedCount, currentTimeMs);
        }

        reporter.ClearProgress();
        blockTimer.Stop();
    }

    /// <summary>
    /// 处理单个块定义内的所有对象
    /// </summary>
    private void ProcessSingleBlockDefinition(Database db, string blockName)
    {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            // 获取块表
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            // 检查块定义是否存在
            if (!bt.Has(blockName))
            {
                tr.Commit();
                return;
            }

            // 获取块定义
            ObjectId blockId = bt[blockName];
            BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

            // 遍历块内的所有 ObjectId
            foreach (ObjectId entityId in blockDef)
            {
                Entity entity = (Entity)tr.GetObject(entityId, OpenMode.ForRead);
                if (entity != null)
                {
                    // 排除属性定义和属性
                    if (!(entity is AttributeDefinition) && !(entity is AttributeReference))
                    {
                        // 先检查图层是否锁定，避免以写模式打开时抛异常
                        bool layerIsLocked = false;
                        try
                        {
                            LayerTableRecord layerRec = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                            layerIsLocked = layerRec.IsLocked;
                        }
                        catch
                        {
                            // 无法检查图层状态，继续尝试修改
                        }

                        if (layerIsLocked)
                        {
                            s_skippedCount++;
                            s_blockSkippedCount++;
                            continue;
                        }

                        // 以写模式打开实体进行修改
                        Entity entityToModify = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);
                        if (ColorHelper.SetToByLayer(entityToModify, tr))
                        {
                            s_modifiedCount++;
                            s_blockModifiedCount++;
                        }
                        else
                        {
                            s_skippedCount++;
                            s_blockSkippedCount++;
                        }
                    }
                }
            }

            tr.Commit();
        }
    }

    /// <summary>
    /// 输出处理结果统计
    /// </summary>
    private void OutputResults(Editor ed, long elapsedMs)
    {
        int totalProcessed = s_modifiedCount + s_skippedCount;
        double elapsedSeconds = elapsedMs / 1000.0;

        string results = $@"
========================================
处理完成！（耗时 {elapsedSeconds:F2} 秒）
  - 实际修改： {s_modifiedCount} 个对象
  - 已跳过：   {s_skippedCount} 个对象（原是ByLayer）
  - 总计处理： {totalProcessed} 个对象

块内部处理详情：
  - 修改块内对象： {s_blockModifiedCount} 个
  - 跳过块内对象： {s_blockSkippedCount} 个
========================================
";
        ed.WriteMessage(results);
    }
}
