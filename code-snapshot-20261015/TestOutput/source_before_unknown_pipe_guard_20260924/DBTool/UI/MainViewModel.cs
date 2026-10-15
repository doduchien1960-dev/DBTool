using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DBTool.Core;
using DBTool.IO;

namespace DBTool.UI
{
    public class ManholeItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public NormalizedManholeSpec Spec { get; init; }
        public string Name => Spec.Name;
        public string Dimensions => $"{Spec.L:F0}x{Spec.B:F0}x{Spec.H:F0}";
        public string Thicknesses => $"Đ={Spec.BaseThickness:F0}, T={Spec.WallThickness:F0}, Tr={Spec.TopThickness:F0}";
        public string PipesSummary
        {
            get
            {
                var p = new List<string>();
                if (Spec.PipeT1 != null) p.Add($"T1:{Spec.PipeT1.PipeCode}");
                if (Spec.PipeT2 != null) p.Add($"T2:{Spec.PipeT2.PipeCode}");
                if (Spec.PipeT3 != null) p.Add($"T3:{Spec.PipeT3.PipeCode}");
                if (Spec.PipeT4 != null) p.Add($"T4:{Spec.PipeT4.PipeCode}");
                return p.Count > 0 ? string.Join(", ", p) : "Không có cống";
            }
        }
        public string NeckSummary => Spec.NeckHeight > 0 ? $"Cổ cao {Spec.NeckHeight:F0}" : "Nắp phẳng (W=0)";

        public ManholeItemViewModel(NormalizedManholeSpec spec)
        {
            Spec = spec;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private string _excelFilePath = "";
        public string ExcelFilePath
        {
            get => _excelFilePath;
            set { _excelFilePath = value; OnPropertyChanged(); }
        }

        private string _selectedSheet = "Tự động nhận diện";
        public string SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> AvailableSheets { get; } = new()
        {
            "Tự động nhận diện",
            "GaThamLienKhoi",
            "GaThamNapRoi",
            "GaThamThuNapRoi"
        };

        private bool _exportExcelSteel = true;
        public bool ExportExcelSteel
        {
            get => _exportExcelSteel;
            set { _exportExcelSteel = value; OnPropertyChanged(); }
        }

        private bool _syncDuplicateSource = false;
        public bool SyncDuplicateSource
        {
            get => _syncDuplicateSource;
            set { _syncDuplicateSource = value; OnPropertyChanged(); }
        }

        private double _baseX = 0;
        public double BaseX
        {
            get => _baseX;
            set { _baseX = value; OnPropertyChanged(); }
        }

        private double _baseY = 0;
        public double BaseY
        {
            get => _baseY;
            set { _baseY = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Sẵn sàng. Hãy chọn tệp Excel và bấm 'Đọc Dữ Liệu'.";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private int _progressValue = 0;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ManholeItemViewModel> Manholes { get; } = new();
        public ObservableCollection<Diagnostic> Diagnostics { get; } = new();

        public ExcelWorkbookReader.ReadResult? CurrentReadResult { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void LoadWorkbook(string filePath, string? sheetChoice)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage = "Tệp không tồn tại!";
                return;
            }

            IsBusy = true;
            StatusMessage = "Đang đọc và kiểm tra cấu trúc bảng tính Excel...";
            Diagnostics.Clear();
            Manholes.Clear();

            try
            {
                string? targetSheet = (sheetChoice == "Tự động nhận diện" || string.IsNullOrWhiteSpace(sheetChoice))
                    ? null
                    : sheetChoice;

                var reader = new ExcelWorkbookReader();
                var result = reader.Read(filePath, targetSheet);
                CurrentReadResult = result;

                foreach (var d in result.Diagnostics)
                {
                    Diagnostics.Add(d);
                }

                foreach (var m in result.Manholes)
                {
                    Manholes.Add(new ManholeItemViewModel(m));
                }

                int blockErrors = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Block);
                if (blockErrors > 0)
                {
                    StatusMessage = $"Đọc được {result.Manholes.Count} hố ga nhưng có {blockErrors} lỗi chặn (Block) cần sửa!";
                }
                else
                {
                    StatusMessage = $"Đọc thành công {result.Manholes.Count} hố ga ({result.DetectedType}). Không có lỗi chặn!";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi đọc file: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SelectAll(bool select)
        {
            foreach (var m in Manholes)
            {
                m.IsSelected = select;
            }
        }
    }
}
