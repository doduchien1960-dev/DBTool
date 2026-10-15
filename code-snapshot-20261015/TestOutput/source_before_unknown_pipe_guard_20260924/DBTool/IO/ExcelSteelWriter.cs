using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using DBTool.Core;

namespace DBTool.IO
{
    /// <summary>
    /// Xuất bản dữ liệu thép ra Excel an toàn:
    /// 1. Sinh tệp 'KL thép [Tên ga].xlsx' từ template VHG-DataForm.xlsx (gồm sheet TH và các sheet chi tiết ga)
    /// 2. Cập nhật bản sao workbook nguồn (sheet Thep) nếu người dùng yêu cầu, tuyệt đối không sửa file gốc
    /// </summary>
    public class ExcelSteelWriter
    {
        public static string ExportSteelScheduleWorkbook(
            string templatePath,
            string outputDirectory,
            string projectOrManholeName,
            List<(NormalizedManholeSpec Spec, SteelScheduleModel Schedule)> manholes)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Không tìm thấy template Excel: {templatePath}");
            }

            Directory.CreateDirectory(outputDirectory);
            string outFileName = $"KL thép {projectOrManholeName}.xlsx";
            string outputPath = Path.Combine(outputDirectory, outFileName);

            using var workbook = new XLWorkbook(templatePath);

            var wsTH = workbook.Worksheet("TH");
            var wsTemp = workbook.Worksheet("Temp");

            int thRow = 5;

            foreach (var (spec, schedule) in manholes)
            {
                string sheetName = spec.Name.Trim();
                // Tên sheet trong Excel không quá 31 ký tự và không chứa ký tự cấm: \ / ? * : [ ]
                sheetName = CleanSheetName(sheetName);

                // Clone sheet Temp
                var wsManhole = wsTemp.CopyTo(sheetName);

                // Ghi tiêu đề
                wsManhole.Cell(1, 1).Value = $"BẢNG THỐNG KÊ CỐT THÉP HỐ GA {sheetName.ToUpper()}";

                // Ghi tên ga vào L4 để các công thức và sheet TH tham chiếu
                wsManhole.Cell(4, 12).Value = sheetName;

                // Ghi các dòng thanh thép bắt đầu từ hàng 5
                int r = 5;
                for (int i = 0; i < schedule.Items.Count; i++)
                {
                    var item = schedule.Items[i];

                    wsManhole.Cell(r, 1).Value = i + 1; // A: TT
                    wsManhole.Cell(r, 2).Value = item.ComponentGroup; // B: Cấu kiện
                    wsManhole.Cell(r, 3).Value = item.BarMark;        // C: Số hiệu

                    if (item.IsAngleSteel)
                    {
                        wsManhole.Cell(r, 4).Value = item.AngleCode; // D: Quy cách thép góc
                    }
                    else
                    {
                        wsManhole.Cell(r, 4).Value = item.Diameter;  // D: Đường kính mm
                    }

                    wsManhole.Cell(r, 5).Value = item.Quantity;      // E: Số thanh
                    wsManhole.Cell(r, 6).Value = item.LengthMm;      // F: Chiều dài 1 thanh mm

                    // Công thức chuẩn theo VHG template
                    wsManhole.Cell(r, 7).FormulaA1 = $"=+F{r}*E{r}/1000";
                    if (item.IsAngleSteel)
                    {
                        wsManhole.Cell(r, 8).Value = item.UnitWeightKgM;
                    }
                    else
                    {
                        wsManhole.Cell(r, 8).FormulaA1 = $"=+PI()*((D{r}/1000)^2)*7850/4";
                    }
                    wsManhole.Cell(r, 9).FormulaA1 = $"=+H{r}*G{r}";
                    wsManhole.Cell(r, 10).FormulaA1 = $"=+LEFT(B{r},2)";

                    r++;
                }

                // Xóa dữ liệu mẫu thừa từ r đến 150 nếu có
                for (int emptyRow = r; emptyRow <= 150; emptyRow++)
                {
                    wsManhole.Row(emptyRow).Clear();
                }

                // Cập nhật dòng tương ứng trong sheet TH
                wsTH.Cell(thRow, 1).Value = thRow - 4; // A: TT
                wsTH.Cell(thRow, 2).FormulaA1 = $"='{sheetName}'!L4";
                wsTH.Cell(thRow, 3).FormulaA1 = $"='{sheetName}'!M4";
                wsTH.Cell(thRow, 4).FormulaA1 = $"='{sheetName}'!N4";
                wsTH.Cell(thRow, 5).FormulaA1 = $"='{sheetName}'!O4";
                wsTH.Cell(thRow, 6).FormulaA1 = $"='{sheetName}'!P4";
                wsTH.Cell(thRow, 7).FormulaA1 = $"='{sheetName}'!Q4";
                wsTH.Cell(thRow, 8).FormulaA1 = $"='{sheetName}'!R4";
                wsTH.Cell(thRow, 9).FormulaA1 = $"='{sheetName}'!S4";
                wsTH.Cell(thRow, 10).FormulaA1 = $"='{sheetName}'!T4";
                wsTH.Cell(thRow, 11).FormulaA1 = $"='{sheetName}'!U4";
                wsTH.Cell(thRow, 12).FormulaA1 = $"='{sheetName}'!V4";
                wsTH.Cell(thRow, 13).FormulaA1 = $"='{sheetName}'!W4";
                wsTH.Cell(thRow, 14).FormulaA1 = $"='{sheetName}'!X4";
                wsTH.Cell(thRow, 15).FormulaA1 = $"='{sheetName}'!Y4";
                wsTH.Cell(thRow, 16).FormulaA1 = $"=SUM(C{thRow}:O{thRow})";

                thRow++;
            }

            // Xóa sheet Temp gốc để tệp đầu ra chỉ gồm TH và các sheet ga thực tế
            wsTemp.Delete();

            workbook.SaveAs(outputPath);
            return outputPath;
        }

        public static string SyncToDuplicateSourceWorkbook(
            string originalSourcePath,
            string outputDirectory,
            List<(NormalizedManholeSpec Spec, SteelScheduleModel Schedule)> manholes)
        {
            if (!File.Exists(originalSourcePath))
            {
                throw new FileNotFoundException($"Không tìm thấy tệp nguồn: {originalSourcePath}");
            }

            Directory.CreateDirectory(outputDirectory);
            string baseName = Path.GetFileNameWithoutExtension(originalSourcePath);
            string ext = Path.GetExtension(originalSourcePath);
            string duplicatePath = Path.Combine(outputDirectory, $"{baseName}_DBTool{ext}");

            // Tạo bản sao độc lập, KHÔNG BAO GIỜ chạm vào file gốc
            File.Copy(originalSourcePath, duplicatePath, overwrite: true);

            using var workbook = new XLWorkbook(duplicatePath);

            if (workbook.TryGetWorksheet("Thep", out var wsThep))
            {
                int startRow = 5;
                for (int i = 0; i < manholes.Count; i++)
                {
                    int r = startRow + i;
                    var (spec, schedule) = manholes[i];

                    wsThep.Cell(r, 1).Value = i + 1;
                    wsThep.Cell(r, 2).Value = spec.Name;
                    wsThep.Cell(r, 3).Value = schedule.Summary.Day1;
                    wsThep.Cell(r, 4).Value = schedule.Summary.Day2;
                    wsThep.Cell(r, 5).Value = schedule.Summary.Tuong1;
                    wsThep.Cell(r, 6).Value = schedule.Summary.Tuong2;
                    wsThep.Cell(r, 7).Value = schedule.Summary.Tran1;
                    wsThep.Cell(r, 8).Value = schedule.Summary.Tran2;
                    wsThep.Cell(r, 9).Value = schedule.Summary.Co1;
                    wsThep.Cell(r, 10).Value = schedule.Summary.Co2;
                    wsThep.Cell(r, 11).Value = schedule.Summary.CuaThu1;
                    wsThep.Cell(r, 12).Value = schedule.Summary.CuaThu2;
                    wsThep.Cell(r, 13).Value = schedule.Summary.Nap1;
                    wsThep.Cell(r, 14).Value = schedule.Summary.Nap2;
                    wsThep.Cell(r, 15).Value = schedule.Summary.ThepHinh;
                }

                workbook.Save();
            }

            return duplicatePath;
        }

        private static string CleanSheetName(string name)
        {
            char[] invalid = { '\\', '/', '?', '*', ':', '[', ']' };
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            if (name.Length > 31) name = name.Substring(0, 31);
            return name;
        }
    }
}
