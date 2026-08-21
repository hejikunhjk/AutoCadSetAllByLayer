// filepath: SetBL/SetBL/SetBLPlugin.cs
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;

namespace SetBL;

/// <summary>
/// SetBL 插件入口类
/// 负责插件的初始化和卸载
/// </summary>
public class SetBLPlugin : IExtensionApplication
{
    /// <summary>
    /// 插件初始化时调用
    /// </summary>
    public void Initialize()
    {
        Editor ed = Autodesk.AutoCAD.ApplicationServices.Application
            .DocumentManager.MdiActiveDocument?.Editor;

        if (ed != null)
        {
            ed.WriteMessage("\nSetBL 插件已加载。输入 'SetBL' 运行。");
        }
    }

    /// <summary>
    /// 插件卸载时调用
    /// </summary>
    public void Terminate()
    {
        // 清理资源（如有需要）
    }
}
