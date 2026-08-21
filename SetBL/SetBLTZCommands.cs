// filepath: SetBL/SetBL/SetBLTZCommands.cs
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
/// SetBLTZ 命令类
/// 功能：测试将天正建筑对象的颜色也改为 ByLayer
/// </summary>
public class SetBLTZCommands
{
    // 统计变量
    private static int s_modifiedCount = 0;       // 实际修改的对象数
    private static int s_skippedCount = 0;        // 跳过的对象数
    private static int s_byLayerCount = 0;        // 原本就是ByLayer的对象数
    private static int s_tarchObjectCount = 0;   // 天正对象数
    private static int s_unknownTypeCount = 0;    // 未知类型对象数
    private static int s_dimTextColorModified = 0; // 标注文字颜色修改数

    // 天正对象类型名称前缀（常见的）
    private static readonly HashSet<string> TarchObjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 建筑对象
        "WALL",           // 墙体
        "WALLLINE",       // 墙线
        "WINDOW",         // 窗户
        "DOOR",           // 门
        "ARCH",           // 圆弧窗/拱门
        "STAIRS",         // 楼梯
        "RAILING",        // 栏杆
        "ELEVATOR",       // 电梯
        
        // 标注对象
        "DIMENSION",      // 尺寸标注
        "LEADER",         // 引线
        "TEXT",           // 单行文字
        "MTEXT",          // 多行文字
        
        // 房间对象
        "ROOM",           // 房间
        "ROOMTAG",        // 房间标注
        
        // 屋顶/板
        "ROOF",           // 屋顶
        "SLAB",           // 楼板
        "FLOOR",          // 地面
        
        // 其他天正对象
        "BALCONY",        // 阳台
        "FLUE",           // 烟囱/风道
        "STAIR",          // 楼梯（另一种拼写）
        "WINDOWSEAT",     // 窗台
        "EQUIPMENT",      // 设备
        
        // 扩展：标准AutoCAD对象（也会检测）
        "LINE",
        "CIRCLE",
        "ARC",
        "LWPOLYLINE",
        "POLYLINE",
        "ELLIPSE",
        "SPLINE",
        "HATCH",
        "SOLID",
        "POINT",
        "SHAPE",
        "INSERT",         // 块引用
    };

    /// <summary>
    /// SetBLTZ 命令 - 主入口
    /// 尝试将包括天正对象在内的所有对象颜色设为 ByLayer
    /// </summary>
    [CommandMethod("SetBLTZ")]
    public void SetAllByColorWithTArch()
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
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\nSetBLTZ - 天正对象颜色测试版本");
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n正在遍历图纸中所有对象...\n");

            Stopwatch sw = Stopwatch.StartNew();

            // --- 处理所有空间（模型+图纸+块定义）---
            ProcessAllObjects(db, ed);

            sw.Stop();

            // 输出详细结果
            OutputResults(ed, sw.ElapsedMilliseconds);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\n错误: {ex.Message}");
            ed.WriteMessage($"\n堆栈: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 重置所有计数器
    /// </summary>
    private static void ResetCounters()
    {
        s_modifiedCount = 0;
        s_skippedCount = 0;
        s_byLayerCount = 0;
        s_tarchObjectCount = 0;
        s_unknownTypeCount = 0;
        s_dimTextColorModified = 0;
    }

    /// <summary>
    /// 处理所有对象
    /// </summary>
    private void ProcessAllObjects(Database db, Editor ed)
    {
        // 第1步：处理模型空间和图纸空间
        ed.WriteMessage("\n>> 第1步：处理模型空间和图纸空间...\n");
        ProcessModelAndPaperSpace(db, ed);

        // 第2步：处理所有块定义
        ed.WriteMessage("\n>> 第2步：处理所有块定义...\n");
        ProcessAllBlockDefinitions(db, ed);
    }

    /// <summary>
    /// 处理模型空间和图纸空间中的所有对象
    /// </summary>
    private void ProcessModelAndPaperSpace(Database db, Editor ed)
    {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId blockId in bt)
            {
                BlockTableRecord blockRec = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                // 只处理布局（模型空间和图纸空间）
                if (!blockRec.IsLayout)
                    continue;

                string layoutName = blockRec.Name;
                ed.WriteMessage($"  处理布局: {layoutName}\n");

                // 遍历所有实体
                foreach (ObjectId entityId in blockRec)
                {
                    Entity entity = (Entity)tr.GetObject(entityId, OpenMode.ForRead);
                    if (entity != null)
                    {
                        ProcessSingleEntity(entity, entityId, ed, tr);
                    }
                }
            }

            tr.Commit();
        }
    }

    /// <summary>
    /// 处理所有块定义
    /// </summary>
    private void ProcessAllBlockDefinitions(Database db, Editor ed)
    {
        List<string> blockNames = new List<string>();

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId blockId in bt)
            {
                BlockTableRecord blockRec = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                // 排除布局
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
        ed.WriteMessage($"  发现 {totalBlocks} 个块定义。\n");

        foreach (string blockName in blockNames)
        {
            ProcessSingleBlockDefinition(db, blockName, ed);
        }
    }

    /// <summary>
    /// 处理单个块定义
    /// </summary>
    private void ProcessSingleBlockDefinition(Database db, string blockName, Editor ed)
    {
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            if (!bt.Has(blockName))
            {
                tr.Commit();
                return;
            }

            ObjectId blockId = bt[blockName];
            BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

            foreach (ObjectId entityId in blockDef)
            {
                Entity entity = (Entity)tr.GetObject(entityId, OpenMode.ForRead);
                if (entity != null)
                {
                    ProcessSingleEntity(entity, entityId, ed, tr);
                }
            }

            tr.Commit();
        }
    }

    /// <summary>
    /// 处理单个实体
    /// </summary>
    private void ProcessSingleEntity(Entity entity, ObjectId entityId, Editor ed, Transaction tr)
    {
        string entityType = entity.GetType().Name;
        bool isTArch = IsTArchObject(entityType);
        bool isByLayer = ColorHelper.IsByLayer(entity);
        
        // 统计天正对象
        if (isTArch)
        {
            s_tarchObjectCount++;
        }

        // 先检查图层是否锁定
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

        // 如果图层被锁定，跳过
        if (layerIsLocked)
        {
            s_skippedCount++;
            return;
        }

        // 如果已经是 ByLayer，跳过
        if (isByLayer)
        {
            s_byLayerCount++;
            s_skippedCount++;
        }
        else
        {
            // 尝试修改颜色
            // 以写模式打开
            Entity entityToModify = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);

            // 检查修改后的颜色是否真的是 ByLayer
            bool wasModified = ColorHelper.SetToByLayer(entityToModify, tr);

            if (wasModified)
            {
                s_modifiedCount++;
            }
            else
            {
                s_skippedCount++;
            }
        }

        // 处理天正标注的文字颜色 XData
        if (entityType == "DIMENSION" || isTArch)
        {
            TryModifyTArchDimTextColor(entity, entityId, tr);
        }
    }

    /// <summary>
    /// 尝试修改天正标注的文字颜色 XData
    /// </summary>
    private void TryModifyTArchDimTextColor(Entity entity, ObjectId entityId, Transaction tr)
    {
        try
        {
            // 获取实体的 XData
            ResultBuffer xdata = entity.XData;
            if (xdata == null)
                return;

            // 天正 XData 应用程序名通常是 "TCH" 或 "TCH20X"
            bool hasTchXData = false;
            bool textColorModified = false;

            // 检查是否有天正 XData
            foreach (TypedValue tv in xdata)
            {
                if (tv.TypeCode == 1001) // DxfCode.RegAppName
                {
                    string appName = tv.Value as string;
                    if (appName != null && (appName.StartsWith("TCH") || appName.StartsWith("TArch")))
                    {
                        hasTchXData = true;
                        break;
                    }
                }
            }

            if (!hasTchXData)
                return;

            // 重新以写模式打开实体来处理 XData
            Entity entityToModify = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);

            // 天正标注文字颜色的 XData 通常组码是 1070（整数）
            // 白色通常是 7，ByLayer 在天正中可能是 0 或某个特殊值
            // 天正的颜色值：0=ByBlock, 256=ByLayer, 其他=直接颜色
            
            // 尝试修改 XData 中的文字颜色
            ResultBuffer newXData = new ResultBuffer();
            bool needUpdate = false;

            foreach (TypedValue tv in xdata)
            {
                if (tv.TypeCode == 1070) // 文字颜色组码
                {
                    short colorValue = Convert.ToInt16(tv.Value);
                    // 如果不是 ByLayer (256 或 0)，则修改
                    if (colorValue != 256 && colorValue != 0)
                    {
                        // 改为 ByLayer (256)
                        newXData.Add(new TypedValue(1070, (short)256));
                        textColorModified = true;
                        needUpdate = true;
                    }
                    else
                    {
                        newXData.Add(tv);
                    }
                }
                else
                {
                    newXData.Add(tv);
                }
            }

            if (needUpdate && textColorModified)
            {
                entityToModify.XData = newXData;
                s_dimTextColorModified++;
            }
        }
        catch (System.Exception)
        {
            // XData 处理失败，忽略
        }
    }

    /// <summary>
    /// 判断对象是否为天正对象
    /// </summary>
    private static bool IsTArchObject(string entityType)
    {
        // 检查是否在天正对象列表中
        return TarchObjectTypes.Contains(entityType);
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
SetBLTZ 测试完成！（耗时 {elapsedSeconds:F2} 秒）
========================================
处理统计：
  - 实体颜色修改： {s_modifiedCount} 个对象
  - 已跳过：       {s_skippedCount} 个对象
  - 原本已是ByLayer: {s_byLayerCount} 个对象
  - 总计处理：     {totalProcessed} 个对象

对象类型统计：
  - 天正相关对象: {s_tarchObjectCount} 个

天正标注文字颜色：
  - 文字颜色修改: {s_dimTextColorModified} 个
========================================
";
        ed.WriteMessage(results);
    }
}
