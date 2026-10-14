using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using DBTool.App;
using DBTool.Cad;
using DBTool.Core;
using DBTool.IO;
using System.Threading;
using System.Security.Cryptography;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Template;
using DBTool.AutoCAD.Render;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Acad = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.UI
{
    /// <summary>
    /// Interaction logic for MainPaletteControl.xaml
    /// Modern WPF Control hosted inside AutoCAD 2026 PaletteSet via PaletteSet.AddVisual
    /// </summary>
    public partial class MainPaletteControl : UserControl
    {
        public MainViewModel ViewModel { get; }

        public MainPaletteControl()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn tệp dữ liệu hố ga Excel",
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                ViewModel.ExcelFilePath = dlg.FileName;
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.ExcelFilePath) || !File.Exists(ViewModel.ExcelFilePath))
            {
                MessageBox.Show("Vui lòng chọn tệp Excel hợp lệ trước khi đọc dữ liệu.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ViewModel.LoadWorkbook(ViewModel.ExcelFilePath, ViewModel.SelectedSheet);
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAll(true);
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAll(false);
        }

        private async void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanGenerate) return;
            int committedCount = 0;
            try
            {
                var selectedSpecs = ViewModel.GetSelectedSpecs().ToList();
                var doc = Acad.DocumentManager.MdiActiveDocument;
                if (doc is null) throw new InvalidOperationException("Không có bản vẽ AutoCAD hiện hành.");
                if (ViewModel.SyncDuplicateSource)
                    throw new InvalidOperationException("Luồng Clean-Room chưa hỗ trợ đồng bộ bản sao nguồn từ FullRebar. Bỏ tùy chọn đồng bộ để vẽ; chức năng này cần package Full đã nghiệm thu.");

                // Freeze presentation and document identity before crossing the async boundary.
                var repository = new GenerationRepository();
                string documentToken = repository.GetDocumentToken(doc);
                string sourcePath = ViewModel.ExcelFilePath;
                string sourceHash = ViewModel.SnapshotInputHash;
                double baseX = ViewModel.BaseX, baseY = ViewModel.BaseY;
                bool exportExcel = ViewModel.ExportExcelSteel;
                var profile = Enum.Parse<DBTool.Domain.Profile>(ViewModel.SelectedProfile);
                string sheet = ViewModel.SelectedSheet == "Tự động nhận diện" ? profile.ToString() : ViewModel.SelectedSheet;
                ViewModel.IsBusy = true;
                ViewModel.ProgressValue = 0;
                ViewModel.StatusMessage = "Đang tính toán các ga đã chọn bằng Clean-Room...";

                var elevMode = Enum.TryParse<DBTool.Domain.ElevationMode>(ViewModel.ElevationMode, out var em) ? em : DBTool.Domain.ElevationMode.Symbolic;
                var presentation = new PresentationRequest(ViewModel.DrawingScale, elevMode, null, "VHG-Dark", true);

                var calculation = await new PaletteDrawingCalculation().CalculateAsync(sourcePath, profile, sheet,
                    selectedSpecs.Select(m => m.Name), sourceHash, presentation, CancellationToken.None);
                if (!calculation.Success)
                {
                    var report=PaletteDrawingCalculation.FormatDiagnostics(calculation.Diagnostics);
                    if(!string.IsNullOrWhiteSpace(report.Review))
                        MessageBox.Show(report.Review,"DBTool - Review (không phải lỗi chặn)",MessageBoxButton.OK,MessageBoxImage.Information);
                    if(!string.IsNullOrWhiteSpace(report.Blocking)) throw new InvalidOperationException(report.Blocking);
                    ViewModel.StatusMessage="Tính toán chưa trả về package. Kiểm tra các thông báo Review.";
                    return;
                }
                var batch = calculation.Value!;
                var roles = batch.Manholes.SelectMany(m => m.Package.DrawingPackage!.Primitives)
                    .OfType<BlockPrimitive>().GroupBy(p => p.BlockRole, StringComparer.Ordinal)
                    .Select(group => new TemplateRequirement(group.Key, AssetKind.Block,
                        group.SelectMany(p => p.Attributes).Select(a => a.Key).Distinct(StringComparer.OrdinalIgnoreCase)))
                    .ToArray();
                string templatePath = TemplateManager.ResolveTemplatePath();
                string templateHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(templatePath)));
                var manifest = new TemplateManifestReader().Read(new(templatePath, templateHash, roles,
                    "VHGpro 3.2/Template/VHG-Template.dwg"));
                if (!manifest.Success) throw new InvalidOperationException(string.Join("\n", manifest.Diagnostics.Select(d => d.Code + ": " + d.Message)));
                var validated = new TemplateContractValidator().Validate(manifest.Value!, roles, TemplateValidationMode.ProductionRender);
                if (!validated.Success) throw new InvalidOperationException(string.Join("\n", validated.Diagnostics.Select(d => d.Code + ": " + d.Message)));

                var generation = new DBTool.Application.GenerateDrawingUseCase(
                    new DBTool.AutoCAD.CadGenerationWriter(doc, repository, new CadRenderer()), validated.Value!);
                var exporter = new DBTool.Application.ExportCalculationUseCase(new SteelWorkbookWriter());
                var exportedPaths = new List<string>();
                string outputDirectory = Path.GetDirectoryName(sourcePath)!;
                double nextY = baseY;
                Exception? commandError = null;

                // Modeless palette: marshal all Autodesk access into the document command context.
                await Acad.DocumentManager.ExecuteInCommandContextAsync(async _ =>
                {
                    try
                    {
                    if (!ReferenceEquals(Acad.DocumentManager.MdiActiveDocument, doc) || repository.GetDocumentToken(doc) != documentToken)
                        throw new InvalidOperationException("DOC_CONTEXT_CHANGED: Bản vẽ hiện hành đã thay đổi.");
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))), sourceHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("SOURCE_SNAPSHOT_MISMATCH: Excel đã thay đổi; đọc lại dữ liệu.");
                    // Template import is a separate setup transaction; entity generation owns its own lock/transaction.
                    using (doc.LockDocument()) TemplateManager.EnsureTemplateLoaded(doc.Database);
                    foreach (var manhole in batch.Manholes)
                    {
                        if (!ReferenceEquals(Acad.DocumentManager.MdiActiveDocument, doc))
                            throw new InvalidOperationException("DOC_CONTEXT_CHANGED: Bản vẽ hiện hành đã thay đổi.");
                        var package = manhole.Package;
                        var request = new DBTool.Application.GenerationRequest(DBTool.Application.GenerationMode.Create,
                            documentToken, null, package.InputHash, new(baseX, nextY));
                        var result = await generation.GenerateAsync(package, request,
                            presentation, CancellationToken.None);
                        if (!result.Success || result.Value?.Committed != true)
                            throw new InvalidOperationException(string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
                        committedCount++;
                        ViewModel.ProgressValue = committedCount * 100 / batch.Manholes.Count;
                        ViewModel.StatusMessage = $"Đã tạo hình {manhole.Name} ({committedCount}/{batch.Manholes.Count}).";
                        var bounds = package.DrawingPackage!.Bounds!;
                        nextY -= Math.Max(LayoutArranger.FrameHeight, bounds.MaxY - bounds.MinY) + 1000;
                        if (exportExcel)
                        {
                            var exported = await exporter.ExportAsync(package, new(outputDirectory, null,
                                "KL thép " + manhole.Name, Guid.NewGuid().ToString("N")), CancellationToken.None);
                            if (!exported.Success)
                                throw new InvalidOperationException("CAD đã tạo; xuất Excel lỗi: " + string.Join("; ", exported.Diagnostics.Select(d => d.Code + ": " + d.Message)));
                            exportedPaths.Add(exported.Value!.Path);
                        }
                    }
                    doc.Editor.Regen();
                    doc.Editor.WriteMessage($"\n[DBTool] Clean-Room: {committedCount} ga; đối chứng 5 tờ A3 còn chờ nghiệm thu.\n");
                    }
                    catch (Exception ex) { commandError = ex; }
                }, null);
                if (commandError is not null) throw new InvalidOperationException(commandError.Message, commandError);
                ViewModel.StatusMessage = $"Clean-Room đã tạo {committedCount} ga, {exportedPaths.Count} file Excel. Bố cục A3 còn chờ nghiệm thu.";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Đã ghi CAD {committedCount} ga. Lỗi: {ex.Message}";
                MessageBox.Show(ViewModel.StatusMessage, "DBTool - Kết quả tạo bản vẽ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { ViewModel.IsBusy = false; }
        }

        private async void BtnExportExcelOnly_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanGenerate) return;
            try
            {
                if (ViewModel.CurrentReadResult == null || ViewModel.Manholes.Count == 0)
                {
                    MessageBox.Show("Chưa có dữ liệu hố ga. Hãy bấm 'Đọc Dữ Liệu & Kiểm Tra' trước.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedSpecs = ViewModel.GetSelectedSpecs().ToList();

                if (selectedSpecs.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất một hố ga để xuất Excel.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ViewModel.IsBusy = true;
                ViewModel.StatusMessage = "Đang chiết tính và xuất bảng thống kê cốt thép Excel...";

                string excelExportInfo = await ExportSteelSchedules(selectedSpecs);

                ViewModel.StatusMessage = $"Xuất Excel thành công! {excelExportInfo}";
                MessageBox.Show($"Đã xuất bảng thống kê cốt thép thành công!\n{excelExportInfo}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Lỗi khi xuất Excel: {ex.Message}";
                MessageBox.Show($"Đã xảy ra lỗi khi xuất Excel:\n{ex}", "Lỗi Thực Thi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ViewModel.IsBusy = false;
            }
        }

        private async System.Threading.Tasks.Task<string> ExportSteelSchedules(List<NormalizedManholeSpec> selectedSpecs)
        {
            var blocking=DrawingInputGate.ForExport(selectedSpecs,ViewModel.CurrentReadResult!.Diagnostics);
            if(blocking.Count>0)throw new InvalidOperationException(string.Join("\n",blocking.Select(d=>d.ToString())));
            if(ViewModel.SyncDuplicateSource)
                throw new InvalidOperationException("Đồng bộ bản sao nguồn cần package Full đã nghiệm thu; chưa hỗ trợ trong luồng FullRebar.");
            var profile=Enum.Parse<DBTool.Domain.Profile>(ViewModel.SelectedProfile);
            string sheet=ViewModel.SelectedSheet=="Tự động nhận diện"?profile.ToString():ViewModel.SelectedSheet;
            var calculation=await new PaletteDrawingCalculation().CalculateAsync(ViewModel.ExcelFilePath,profile,sheet,
                selectedSpecs.Select(s=>s.Name),ViewModel.SnapshotInputHash,CancellationToken.None);
            if(!calculation.Success)throw new InvalidOperationException(PaletteDrawingCalculation.FormatDiagnostics(calculation.Diagnostics).Blocking);
            var paths=await SchedulePackageExport.ExportAsync(calculation.Value!,Path.GetDirectoryName(ViewModel.ExcelFilePath)!,CancellationToken.None);
            return string.Join("; ",paths.Select(Path.GetFileName));
        }
        private static string ResolveDataFormPath()
        {
            string[] candidates =
            {
                @"c:\Users\DucBao\Desktop\Dbtool\Ver 1\VHGpro 3.2\Template\VHG-DataForm.xlsx",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template", "VHG-DataForm.xlsx"),
                @"C:\Users\DucBao\Downloads\VHGpro 3.2\VHGpro 3.2\Template\VHG-DataForm.xlsx"
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p)) return p;
            }

            return candidates[0];
        }
    }
}
