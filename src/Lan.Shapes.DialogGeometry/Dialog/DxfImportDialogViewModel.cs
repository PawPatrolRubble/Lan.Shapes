using System;
using Microsoft.Win32;

namespace Lan.Shapes.DialogGeometry.Dialog
{
    public class DxfImportDialogViewModel : DialogViewModelBase
    {
        private double _pixelToMmFactor = 1.0;
        private string _filePath = string.Empty;

        public DxfImportDialogViewModel() : this(1.0)
        {
        }

        public DxfImportDialogViewModel(double pixelToMmFactor)
        {
            if (double.IsNaN(pixelToMmFactor) ||
                double.IsInfinity(pixelToMmFactor) ||
                pixelToMmFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pixelToMmFactor),
                    pixelToMmFactor,
                    "Pixel-to-millimeter factor must be greater than zero.");
            }

            _pixelToMmFactor = pixelToMmFactor;
            BrowseCommand = new RelayCommand(BrowseFile);
        }

        public double PixelToMmFactor
        {
            get => _pixelToMmFactor;
            set => SetField(ref _pixelToMmFactor, value);
        }

        public string FilePath
        {
            get => _filePath;
            set => SetField(ref _filePath, value);
        }

        public RelayCommand BrowseCommand { get; }

        private void BrowseFile()
        {
            var filePicker = new OpenFileDialog
            {
                Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*"
            };

            if (filePicker.ShowDialog() == true)
            {
                FilePath = filePicker.FileName;
            }
        }
    }
}
