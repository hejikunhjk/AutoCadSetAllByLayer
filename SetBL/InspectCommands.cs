// filepath: SetBL/SetBL/InspectCommands.cs
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace SetBL;

/// <summary>
/// InspectCommands - Debug/diagnostic commands for inspecting objects
/// </summary>
public class InspectCommands
{
    /// <summary>
    /// INSPECT command - Inspect selected objects
    /// Usage: Select objects with window selection
    /// </summary>
    // [CommandMethod("INSPECT")]
    public void InspectObjects()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        Database db = doc.Database;
        Editor ed = doc.Editor;

        // Prompt for selection
        PromptSelectionOptions selOpt = new PromptSelectionOptions();
        selOpt.MessageForAdding = "\nSelect objects to inspect: ";
        selOpt.AllowDuplicates = false;

        PromptSelectionResult selResult = ed.GetSelection(selOpt);

        if (selResult.Status != PromptStatus.OK)
        {
            ed.WriteMessage("\nNo objects selected.\n");
            return;
        }

        SelectionSet selSet = selResult.Value;

        ed.WriteMessage($"\n========================================");
        ed.WriteMessage($"\nInspect: {selSet.Count} objects selected");
        ed.WriteMessage($"\n========================================\n");

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            int index = 1;
            foreach (SelectedObject selObj in selSet)
            {
                if (selObj.ObjectId == ObjectId.Null)
                    continue;

                Entity entity = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                if (entity == null)
                    continue;

                OutputEntityInfo(entity, tr, ed, index);
                index++;
            }

            tr.Commit();
        }

        ed.WriteMessage($"\n========================================");
        ed.WriteMessage($"\nDone!");
        ed.WriteMessage($"\n========================================\n");
    }

    /// <summary>
    /// INSPECTX command - Inspect single object XData
    /// Usage: Pick one object
    /// </summary>
    // [CommandMethod("INSPECTX")]
    public void InspectXData()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        Database db = doc.Database;
        Editor ed = doc.Editor;

        PromptEntityOptions opt = new PromptEntityOptions("\nSelect object to inspect: ");
        PromptEntityResult res = ed.GetEntity(opt);

        if (res.Status != PromptStatus.OK)
        {
            ed.WriteMessage("\nNo object selected.\n");
            return;
        }

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            Entity entity = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
            if (entity != null)
            {
                OutputEntityInfo(entity, tr, ed, 1);
                ed.WriteMessage($"\n--- Raw XData Dump ---");
                DumpXDataRaw(entity.XData, ed);
            }

            tr.Commit();
        }
    }

    /// <summary>
    /// Output entity info
    /// </summary>
    private void OutputEntityInfo(Entity entity, Transaction tr, Editor ed, int index)
    {
        Type entityType = entity.GetType();
        string typeName = entityType.Name;
        string typeFullName = entityType.FullName;
        string handle = entity.Handle.ToString();
        
        string layerName = "N/A";
        try { layerName = entity.Layer; } catch { }

        int colorIndex = 0;
        bool isByLayer = false;
        bool isByBlock = false;
        try 
        { 
            colorIndex = (int)entity.ColorIndex; 
            isByLayer = colorIndex == 256;
            isByBlock = colorIndex == 0;
        } catch { }

        string colorInfo = isByLayer ? "ByLayer" : 
                           isByBlock ? "ByBlock" : 
                           $"Index={colorIndex}";

        // 检查是否为天正对象
        bool isTArch = typeFullName != null && (
            typeFullName.Contains("ImpEntity") ||
            typeFullName.Contains("TCH") ||
            typeFullName.Contains("TArch") ||
            typeFullName.Contains("TB")
        );

        ed.WriteMessage($"\n--- Object #{index} ---");
        ed.WriteMessage($"\n  Type: {typeName}{(isTArch ? " [天正]" : "")}");
        ed.WriteMessage($"\n  FullType: {typeFullName}");
        ed.WriteMessage($"\n  Layer: {layerName}");
        ed.WriteMessage($"\n  Handle: {handle}");
        ed.WriteMessage($"\n  Color: {colorInfo}");

        // 检�?XData
        ResultBuffer rb = entity.XData;
        if (rb != null)
        {
            int itemCount = 0;
            foreach (TypedValue tv in rb) { itemCount++; }
            ed.WriteMessage($"\n  XData: Yes ({itemCount} items)");
            OutputXDataByApp(rb, ed);
        }
        else
        {
            ed.WriteMessage($"\n  XData: None");
        }

        // 检�?ExtensionDictionary (天正数据可能存储在这�?
        if (entity.ExtensionDictionary != ObjectId.Null)
        {
            ed.WriteMessage($"\n  ExtensionDictionary: Yes");
            try
            {
                DBDictionary dict = (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
                ed.WriteMessage($"\n    DictEntries: {dict.Count}");
                foreach (DBDictionaryEntry entry in dict)
                {
                    ed.WriteMessage($"\n      [{entry.Key}]");
                }
            }
            catch { }
        }
        else
        {
            ed.WriteMessage($"\n  ExtensionDictionary: None");
        }

        // 检查是否为 Proxy 对象
        try
        {
            if (entity.IsAProxy)
            {
                ed.WriteMessage($"\n  IsProxy: Yes");
                // 尝试获取代理相关信息
                try
                {
                    // 获取实体类型信息
                    ed.WriteMessage($"\n    ClassName: {entity.GetType().Name}");
                    ed.WriteMessage($"\n    ClassFullName: {entity.GetType().FullName}");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n    ClassInfo: {ex.Message}");
                }
            }
            else
            {
                ed.WriteMessage($"\n  IsProxy: No");
            }
        }
        catch { }

        // 尝试获取子对象信息（复合对象内部组件）
        try
        {
            // 使用已存在的entityType变量
            var subents = entityType.GetProperty("Subentities");
            if (subents != null)
            {
                ed.WriteMessage($"\n  Subentities: Yes");
            }
            else
            {
                ed.WriteMessage($"\n  Subentities: No");
            }
        }
        catch { }

        // 通过反射探测所有属性（包括私有）
        try
        {
            ed.WriteMessage($"\n  --- Reflection Properties ---");
            var allProps = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var prop in allProps)
            {
                if (prop.Name.Contains("Color") || prop.Name.Contains("Text") || prop.Name.Contains("Sub"))
                {
                    ed.WriteMessage($"\n    {prop.PropertyType.Name} {prop.Name}");
                }
            }
        }
        catch { }

    }

    /// <summary>
    /// Output XData grouped by app name
    /// </summary>
    private void OutputXDataByApp(ResultBuffer rb, Editor ed)
    {
        Dictionary<string, List<TypedValue>> appData = new Dictionary<string, List<TypedValue>>();
        string currentApp = null;
        List<TypedValue> currentList = null;

        foreach (TypedValue tv in rb)
        {
            if (tv.TypeCode == 1001)
            {
                currentApp = tv.Value as string;
                if (currentApp != null)
                {
                    if (!appData.ContainsKey(currentApp))
                    {
                        appData[currentApp] = new List<TypedValue>();
                    }
                    currentList = appData[currentApp];
                }
            }
            else if (currentList != null)
            {
                currentList.Add(tv);
            }
        }

        foreach (var kvp in appData)
        {
            string appName = kvp.Key;
            List<TypedValue> items = kvp.Value;
            
            bool isTArch = appName.StartsWith("TCH", StringComparison.OrdinalIgnoreCase) ||
                          appName.StartsWith("TArch", StringComparison.OrdinalIgnoreCase) ||
                          appName.StartsWith("TB", StringComparison.OrdinalIgnoreCase);

            string marker = isTArch ? " [TArch]" : "";
            ed.WriteMessage($"\n    App: [{appName}]{marker}");

            foreach (TypedValue tv in items)
            {
                string dxfName = GetDxfName(tv.TypeCode);
                string valueStr = FormatTypedValue(tv.Value);
                ed.WriteMessage($"\n      {dxfName}({tv.TypeCode}): {valueStr}");
            }
        }
    }

    /// <summary>
    /// Dump raw XData
    /// </summary>
    private void DumpXDataRaw(ResultBuffer xdata, Editor ed)
    {
        if (xdata == null)
        {
            ed.WriteMessage("\n  (No XData)");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine();

        int i = 0;
        foreach (TypedValue tv in xdata)
        {
            string dxfName = GetDxfName(tv.TypeCode);
            string valueStr = FormatTypedValue(tv.Value);
            sb.AppendLine($"    [{i:D3}] {dxfName}({tv.TypeCode}): {valueStr}");
            i++;
        }

        ed.WriteMessage(sb.ToString());
    }

    /// <summary>
    /// Get DXF name for group code
    /// </summary>
    private string GetDxfName(int typeCode)
    {
        switch (typeCode)
        {
            case 1000: return "1000";
            case 1001: return "RegAppName";
            case 1002: return "1002";
            case 1003: return "LayerName";
            case 1004: return "BinaryData";
            case 1005: return "Handle";
            case 1010: return "1010";
            case 1011: return "1011";
            case 1012: return "1012";
            case 1013: return "1013";
            case 1020: return "1020";
            case 1021: return "1021";
            case 1022: return "1022";
            case 1023: return "1023";
            case 1024: return "1024";
            case 1025: return "1025";
            case 1070: return "1070(Color)";
            case 1071: return "1071";
            default: return typeCode.ToString();
        }
    }

    /// <summary>
    /// Format typed value for display
    /// </summary>
    private string FormatTypedValue(object value)
    {
        if (value == null)
            return "(null)";

        try
        {
            if (value is byte[] bytes)
            {
                if (bytes.Length <= 16)
                    return "[" + string.Join(",", bytes) + "]";
                else
                    return "[" + bytes.Length + " bytes]";
            }

            if (value is Int16 || value is Int32 || value is Int64)
            {
                long val = Convert.ToInt64(value);
                return $"{val} (0x{val:X4})";
            }

            if (value is UInt16 || value is UInt32 || value is UInt64)
            {
                ulong val = Convert.ToUInt64(value);
                return $"{val} (0x{val:X4})";
            }

            return value.ToString();
        }
        catch
        {
            return "(format error)";
        }
    }
}
