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

                Document? doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
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
            Document? doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
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
            Document? doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
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

                int blockCount = DrawingInputGate.ForBatch(result.Manholes, result.Diagnostics).Count;
                if (blockCount > 0)
                {
                    ed.WriteMessage($"\n[Cảnh báo] Có {blockCount} lỗi chặn trong file Excel. Vui lòng kiểm tra lại dữ liệu!\n");
                    return;
                }

                ed.WriteMessage($"\nĐã nhận diện: {result.DetectedType} với {result.Manholes.Count} hố ga.");

                var profile = result.DetectedType switch {
                    ManholeType.GaThamLienKhoi => DBTool.Domain.Profile.GaThamLienKhoi,
                    ManholeType.GaThamNapRoi => DBTool.Domain.Profile.GaThamNapRoi,
                    ManholeType.GaThamThuNapRoi => DBTool.Domain.Profile.GaThamThuNapRoi,
                    _ => throw new InvalidOperationException("Unsupported profile")
                };
                var presentation = new DBTool.Drawing.PresentationRequest(30, DBTool.Domain.ElevationMode.Symbolic, null, "VHG-Dark", true);
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(excelPath)));
                var calculated=new PaletteDrawingCalculation().CalculateAsync(excelPath,profile,profile.ToString(),
                    result.Manholes.Select(m=>m.Name),hash,presentation,default).GetAwaiter().GetResult();
                if(!calculated.Success)throw new InvalidOperationException(PaletteDrawingCalculation.FormatDiagnostics(calculated.Diagnostics).Blocking);
                var batch=calculated.Value!;
                var roles=batch.Manholes.SelectMany(m=>m.Package.DrawingPackage!.Primitives).OfType<DBTool.Drawing.BlockPrimitive>()
                    .GroupBy(p=>p.BlockRole,StringComparer.Ordinal).Select(g=>new DBTool.Application.Templates.TemplateRequirement(
                        g.Key,DBTool.Application.Templates.AssetKind.Block,g.SelectMany(p=>p.Attributes).Select(a=>a.Key).Distinct(StringComparer.OrdinalIgnoreCase))).ToArray();
                string templatePath=TemplateManager.ResolveTemplatePath();
                string templateHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(templatePath)));
                var manifest=new DBTool.AutoCAD.Template.TemplateManifestReader().Read(new(templatePath,templateHash,roles,"VHGpro 3.2/Template/VHG-Template.dwg"));
                if(!manifest.Success)throw new InvalidOperationException(string.Join("; ",manifest.Diagnostics.Select(d=>d.Code+": "+d.Message)));
                var validated=new DBTool.Application.Templates.TemplateContractValidator().Validate(manifest.Value!,roles,DBTool.Application.Templates.TemplateValidationMode.ProductionRender);
                if(!validated.Success)throw new InvalidOperationException(string.Join("; ",validated.Diagnostics.Select(d=>d.Code+": "+d.Message)));
                var repository=new DBTool.AutoCAD.Generation.GenerationRepository();
                string documentToken=repository.GetDocumentToken(doc);
                var generation=new DBTool.Application.GenerateDrawingUseCase(new DBTool.AutoCAD.CadGenerationWriter(doc,repository,new DBTool.AutoCAD.Render.CadRenderer()),validated.Value!);
                using(doc.LockDocument())TemplateManager.EnsureTemplateLoaded(doc.Database);
                double nextY=0;
                foreach(var manhole in batch.Manholes) {
                    var package=manhole.Package;
                    var request=new DBTool.Application.GenerationRequest(DBTool.Application.GenerationMode.Create,documentToken,null,package.InputHash,new(0,nextY));
                    var generated=generation.GenerateAsync(package,request,presentation,default).GetAwaiter().GetResult();
                    if(!generated.Success || generated.Value?.Committed!=true)throw new InvalidOperationException(string.Join("; ",generated.Diagnostics.Select(d=>d.Code+": "+d.Message)));
                    var bounds=package.DrawingPackage!.Bounds!;
                    nextY-=Math.Max(LayoutArranger.FrameHeight,bounds.MaxY-bounds.MinY)+1000;
                    ed.WriteMessage($"\n[Package] {manhole.Name}: {package.InputHash}; steel kg={package.SteelSchedule!.TotalMassKg.Value:R}");
                }
                if (batch.RowReceipts != null)
                {
                    foreach (var receipt in batch.RowReceipts)
                    {
                        ed.WriteMessage($"\n[Row Receipt] {receipt.Name}: Status={receipt.Status}");
                    }
                }
                var exported=SchedulePackageExport.ExportAsync(batch,Path.GetDirectoryName(excelPath)!,default).GetAwaiter().GetResult();
                foreach(var path in exported)ed.WriteMessage($"\n[Xuất Excel]: {path}");
                ed.WriteMessage("\n[DBTool 2026] Đã tạo package Batch; nghiệm thu PDF golden còn chờ kiểm chứng.\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Lỗi Batch Execution]: {ex}\n");
            }
        }
    }
}
