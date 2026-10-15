using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        private readonly HashSet<ManholeItemViewModel> _observedItems = new();

        public MainViewModel()
        {
            Manholes.CollectionChanged += ManholesChanged;
            Diagnostics.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanGenerate));
        }

        private void ManholesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in _observedItems.Where(item => !Manholes.Contains(item)).ToArray())
            {
                item.PropertyChanged -= SelectionChanged;
                _observedItems.Remove(item);
            }
            foreach (var item in Manholes)
                if (_observedItems.Add(item)) item.PropertyChanged += SelectionChanged;
            if (ActiveManhole is not null && !Manholes.Contains(ActiveManhole)) ActiveManhole = null;
            NotifySelection();
        }

        private void SelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManholeItemViewModel.IsSelected)) NotifySelection();
        }

        private void NotifySelection()
        {
            OnPropertyChanged(nameof(SelectedManholeIds));
            OnPropertyChanged(nameof(CanGenerate));
        }

        private ManholeItemViewModel? _activeManhole;
        public ManholeItemViewModel? ActiveManhole
        {
            get => _activeManhole;
            set { _activeManhole = value; OnPropertyChanged(); NotifySelection(); }
        }

        // Checked rows take priority. With no checkmarks, use the highlighted row.
        public IReadOnlyList<NormalizedManholeSpec> GetSelectedSpecs()
        {
            var selected = Manholes.Where(x => x.IsSelected).Select(x => x.Spec).ToList();
            if (selected.Count == 0 && ActiveManhole is not null && Manholes.Contains(ActiveManhole))
                selected.Add(ActiveManhole.Spec);
            return selected;
        }

        private string _excelFilePath = "";
        public string ExcelFilePath
        {
            get => _excelFilePath;
            set { _excelFilePath = value; IsInputStale = true; OnPropertyChanged(); OnPropertyChanged(nameof(CanGenerate)); }
        }

        private string _selectedSheet = "Tự động nhận diện";
        public string SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; IsInputStale = true; OnPropertyChanged(); OnPropertyChanged(nameof(CanGenerate)); }
        }

        private string _selectedProfile = "GaThamLienKhoi";
        public string SelectedProfile
        {
            get => _selectedProfile;
            set { _selectedProfile = value; IsInputStale = true; OnPropertyChanged(); OnPropertyChanged(nameof(CanGenerate)); }
        }

        public ObservableCollection<string> AvailableProfiles { get; } = new()
        {
            "GaThamLienKhoi",
            "GaThamNapRoi",
            "GaThamThuNapRoi"
        };

        public ObservableCollection<string> AvailableSheets { get; } = new()
        {
            "Tự động nhận diện",
            "GaThamLienKhoi",
            "GaThamNapRoi",
            "GaThamThuNapRoi"
        };

        private double _drawingScale = 30.0;
        public double DrawingScale
        {
            get => _drawingScale;
            set { _drawingScale = value; OnPropertyChanged(); }
        }

        public ObservableCollection<double> AvailableScales { get; } = new() { 20, 25, 30, 40, 50 };

        private string _scaleMode = "Fixed";
        public string ScaleMode
        {
            get => _scaleMode;
            set { _scaleMode = value; OnPropertyChanged(); }
        }

        private string _elevationMode = "Symbolic";
        public string ElevationMode
        {
            get => _elevationMode;
            set { _elevationMode = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> AvailableElevationModes { get; } = new()
        {
            "Symbolic",
            "Absolute",
            "Relative"
        };

        private bool _isInputStale;
        public bool IsInputStale
        {
            get => _isInputStale;
            private set { _isInputStale = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGenerate)); }
        }

        public string SnapshotInputHash { get; private set; } = "";
        public string SelectedManholeIds => string.Join(", ", GetSelectedSpecs().Select(x => x.Name));
        public bool CanGenerate => !IsBusy && !IsInputStale && CurrentReadResult is not null &&
            GetSelectedSpecs().Count > 0 && DrawingInputGate.ForUi(GetSelectedSpecs(), Diagnostics).Count == 0;

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
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGenerate)); }
        }

        public ObservableCollection<ManholeItemViewModel> Manholes { get; } = new();
        public ObservableCollection<Diagnostic> Diagnostics { get; } = new();

        public ExcelWorkbookReader.ReadResult? CurrentReadResult { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void LoadWorkbook(string filePath, string? sheetChoice)
        {
            IsInputStale = true;
            CurrentReadResult = null;
            SnapshotInputHash = "";
            OnPropertyChanged(nameof(SnapshotInputHash));
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
                    ? SelectedProfile
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
                    var item = new ManholeItemViewModel(m);
                    Manholes.Add(item);
                }

                if (result.DetectedType.ToString() != SelectedProfile)
                    throw new InvalidOperationException("Sheet dữ liệu không khớp profile đang chọn. Chọn lại sheet/profile rồi đọc dữ liệu.");
                SnapshotInputHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));
                IsInputStale = false;
                OnPropertyChanged(nameof(SnapshotInputHash));
                OnPropertyChanged(nameof(SelectedManholeIds));

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
            if (!select) ActiveManhole = null;
            foreach (var m in Manholes)
            {
                m.IsSelected = select;
            }
        }
    }
}
