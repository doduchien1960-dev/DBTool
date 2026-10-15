using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using DBTool.Cad;
using DBTool.Core;
using DBTool.IO;
using DBTool.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(DBTool.App.HoGaCommands))]

namespace DBTool.App
{
    /// <summary>
    /// Các lệnh AutoCAD chính của DBTool 2026:
    /// - VEHOGA: Mở bảng điều khiển WPF hiện đại (PaletteSet.AddVisual)
    /// - VEHOGABATCH: Lệnh thực thi trực tiếp qua dòng lệnh / script (hỗ trợ accoreconsole)
    /// </summary>
    public class HoGaCommands : IExtensionApplication
    {
        private static PaletteSet? _paletteSet;
        private static readonly Guid PaletteGuid = new("A8B9C1D2-E3F4-4A5B-8C6D-7E8F9A0B1C2D");

        public void Initialize()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(typeof(HoGaCommands).Assembly.Location) ?? "";
                        string simpleName = new System.Reflection.AssemblyName(args.Name).Name;
                        string dllPath = Path.Combine(folder, simpleName + ".dll");
                        if (File.Exists(dllPath))
                        {
                            return System.Reflection.Assembly.LoadFrom(dllPath);
                        }
                    }
                    catch { }
                    return null;
                };

                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage("\n=======================================================");
                    doc.Editor.WriteMessage("\n[DBTool 2026] Clean-Room Engine for AutoCAD & Civil 3D");
                    doc.Editor.WriteMessage("\n - Gõ 'VEHOGA' để mở bảng điều khiển trực quan.");
                    doc.Editor.WriteMessage("\n - Gõ 'VEHOGABATCH' để chạy tự động theo file Excel.");
                    doc.Editor.WriteMessage("\n=======================================================\n");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DBTool Initialize Error: {ex}");
            }
        }

        public void Terminate()
        {
            try
            {
                if (_paletteSet != null)
                {
                    _paletteSet.Visible = false;
                    _paletteSet.Dispose();
                    _paletteSet = null;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DBTool Terminate Error: {ex}");
            }
        }

        /// <summary>
        /// Lệnh chính: Mở bảng điều khiển Palette WPF hiện đại
        /// </summary>
        [CommandMethod("VEHOGA")]
        public void OpenPalette()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                if (_paletteSet == null)
                {
                    _paletteSet = new PaletteSet("DBTool 2026 — Thiết Kế Hố Ga", PaletteGuid)
                    {
                        Style = PaletteSetStyles.ShowAutoHideButton |
                                PaletteSetStyles.ShowCloseButton |
                                PaletteSetStyles.Snappable,
                        MinimumSize = new System.Drawing.Size(420, 600),
                        Size = new System.Drawing.Size(450, 750)
                    };

                    var control = new MainPaletteControl();
                    _paletteSet.AddVisual("DBTool", control);
                }

                _paletteSet.Visible = true;
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n[Lỗi mở bảng điều khiển DBTool]: {ex.Message}\n");
            }
        }

        /// <summary>
        /// Lệnh dòng lệnh Batch: Dành cho chạy tự động, kịch bản hoặc accoreconsole
        /// Cú pháp: VEHOGABATCH "C:\duongdan\file.xlsx" [sheetName] [baseX] [baseY]
        /// </summary>
        [CommandMethod("VEHOGABATCH")]
        public void RunBatch()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                var promptFile = ed.GetString(new PromptStringOptions("\nNhập đường dẫn file Excel (.xlsx): ")
                {
                    AllowSpaces = true
                });
                if (promptFile.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(promptFile.StringResult))
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                string excelPath = promptFile.StringResult.Trim('"', ' ');
                if (!File.Exists(excelPath))
                {
                    ed.WriteMessage($"\n[Lỗi] Không tìm thấy file: {excelPath}\n");
                    return;
                }

                ed.WriteMessage($"\nĐang đọc dữ liệu từ: {excelPath}...");
                var reader = new ExcelWorkbookReader();
                var result = reader.Read(excelPath);

                int blockCount = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Block);
                if (blockCount > 0)
                {
                    ed.WriteMessage($"\n[Cảnh báo] Có {blockCount} lỗi chặn trong file Excel. Vui lòng kiểm tra lại dữ liệu!\n");
                    return;
                }

                ed.WriteMessage($"\nĐã nhận diện: {result.DetectedType} với {result.Manholes.Count} hố ga.");

                var progress = new Progress<GenerationProgress>(p =>
                {
                    ed.WriteMessage($"\n[{p.CurrentIndex}/{p.TotalCount}] {p.StatusMessage}");
                });

                ManholeOrchestrator.GenerateDrawings(
                    doc,
                    result.Manholes,
                    result,
                    0, 0,
                    progress);

                // Xuất file thép
                string outDir = Path.GetDirectoryName(excelPath) ?? Path.GetTempPath();
                var schedules = new System.Collections.Generic.List<(NormalizedManholeSpec, SteelScheduleModel)>();
                foreach (var spec in result.Manholes)
                {
                    var geom = GeometryEngine.Build(spec);
                    var bars = RebarEngine.GenerateRebarModel(spec, geom, result.AngleCatalog);
                    var sched = ScheduleEngine.CalculateSchedule(spec, bars, result.AngleCatalog);
                    schedules.Add((spec, sched));
                }

                string templateExcel = TemplateManager.ResolveTemplatePath();
                string dataFormExcel = Path.Combine(Path.GetDirectoryName(templateExcel) ?? "", "VHG-DataForm.xlsx");
                if (File.Exists(dataFormExcel))
                {
                    string projectName = result.Manholes.Count == 1 ? result.Manholes[0].Name : $"{result.Manholes[0].Name}_TongHop";
                    string exported = ExcelSteelWriter.ExportSteelScheduleWorkbook(dataFormExcel, outDir, projectName, schedules);
                    ed.WriteMessage($"\n[Xuất Excel]: {exported}");
                }

                ed.WriteMessage("\n[DBTool 2026] Quá trình vẽ Batch hoàn thành thành công!\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Lỗi Batch Execution]: {ex}\n");
            }
        }
    }
}
