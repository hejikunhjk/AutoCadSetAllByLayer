// filepath: SetBL/SetBL/Utils/ColorHelper.cs
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System;

namespace SetBL.Utils;

/// <summary>
/// 颜色处理工具类
/// 提供 ByLayer 颜色判断和设置功能
/// </summary>
public static class ColorHelper
{
    /// <summary>
    /// ByLayer 的颜色索引值
    /// </summary>
    public const short ByLayerColorIndex = 256;

    /// <summary>
    /// 判断对象的颜色是否为 ByLayer
    /// </summary>
    /// <param name="entity">要检查的实体</param>
    /// <returns>如果颜色是ByLayer返回true，否则返回false</returns>
    public static bool IsByLayer(Entity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // 通过 ColorIndex 判断 (256 = ByLayer)
        return entity.ColorIndex == ByLayerColorIndex;
    }

    /// <summary>
    /// 判断对象的颜色索引是否为 ByLayer (256)
    /// </summary>
    /// <param name="entity">要检查的实体</param>
    /// <returns>如果颜色索引是256返回true，否则返回false</returns>
    public static bool IsByLayerByColorIndex(Entity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // ColorIndex 为 256 表示 ByLayer
        return entity.ColorIndex == ByLayerColorIndex;
    }

    /// <summary>
    /// 将对象颜色设置为 ByLayer
    /// </summary>
    /// <param name="entity">要修改的实体</param>
    /// <param name="tr">事务对象（用于检查图层是否锁定）</param>
    /// <returns>如果实际修改了返回true，如果原本就是ByLayer或图层被锁定返回false</returns>
    public static bool SetToByLayer(Entity entity, Transaction tr)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // 先检查图层是否被锁定（图层锁定时访问 ColorIndex 会抛异常）
        if (tr != null)
        {
            try
            {
                LayerTableRecord layerRec = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                if (layerRec.IsLocked)
                    return false; // 图层被锁定，跳过
            }
            catch
            {
                // 如果无法检查图层状态，假设未锁定
            }
        }

        // 检查是否已经是ByLayer
        try
        {
            if (entity.ColorIndex == ByLayerColorIndex)
                return false;
        }
        catch
        {
            // 无法获取颜色，跳过
            return false;
        }

        // 设置 ColorIndex 为 256 (ByLayer)
        try
        {
            entity.ColorIndex = ByLayerColorIndex;
            return true;
        }
        catch
        {
            // 设置失败（图层可能被锁定），跳过
            return false;
        }
    }

    /// <summary>
    /// 将对象颜色设置为 ByLayer（兼容旧调用，无图层锁定检查）
    /// </summary>
    /// <param name="entity">要修改的实体</param>
    /// <returns>如果实际修改了返回true，如果原本就是ByLayer返回false</returns>
    public static bool SetToByLayer(Entity entity)
    {
        return SetToByLayer(entity, null);
    }

    /// <summary>
    /// 将对象颜色设置为 ByLayer（基于颜色索引判断）
    /// </summary>
    /// <param name="entity">要修改的实体</param>
    /// <param name="tr">事务对象（用于检查图层是否锁定）</param>
    /// <returns>如果实际修改了返回true，如果原本就是ByLayer或图层被锁定返回false</returns>
    public static bool SetToByLayerByColorIndex(Entity entity, Transaction tr)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // 先检查图层是否被锁定（图层锁定时访问 ColorIndex 会抛异常）
        if (tr != null)
        {
            try
            {
                LayerTableRecord layerRec = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                if (layerRec.IsLocked)
                    return false; // 图层被锁定，跳过
            }
            catch
            {
                // 如果无法检查图层状态，假设未锁定
            }
        }

        // 如果已经是ByLayer (256)，跳过
        try
        {
            if (entity.ColorIndex == ByLayerColorIndex)
                return false;
        }
        catch
        {
            return false;
        }

        // 设置 ColorIndex 为 256 (ByLayer)
        try
        {
            entity.ColorIndex = ByLayerColorIndex;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将对象颜色设置为 ByLayer（基于颜色索引判断，兼容旧调用）
    /// </summary>
    /// <param name="entity">要修改的实体</param>
    /// <returns>如果实际修改了返回true，如果原本就是ByLayer返回false</returns>
    public static bool SetToByLayerByColorIndex(Entity entity)
    {
        return SetToByLayerByColorIndex(entity, null);
    }
}
