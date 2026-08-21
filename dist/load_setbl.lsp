;;; filepath: dist/load_setbl.lsp
;;; SetBL 插件加载脚本
;;; 用法：在 AutoCAD 命令行运行: (load "load_setbl")
;;; 或将此文件拖入 AutoCAD 窗口

(defun C:LOAD-SETBL (/ dll-path)
  "加载 SetBL.dll 并注册 SetBL 和 SetBLTZ 命令"
  (setq dll-path (findfile "SetBL.dll"))
  (if (null dll-path)
    (progn
      (princ "\n[错误] 找不到 SetBL.dll，请确认文件存在于 AutoCAD 搜索路径中。")
      (princ)
    )
    (progn
      (princ (strcat "\n[SetBL] 正在加载: " dll-path))
      ;; 使用 _.netload 确保调用 AutoCAD 原生命令
      (command "_.netload" dll-path)
      (princ "\n[SetBL] 插件已加载！命令行输入: SetBL")
      (princ)
    )
  )
)

;; 拖入 CAD 时自动加载
(C:LOAD-SETBL)
(princ)
