using Microsoft.Win32;

namespace VChibi.Services;

public class WindowsFilePickerService : IFilePickerService
{
    public IReadOnlyList<string>? PickPngFiles(string title = "Chọn hình ảnh PNG")
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "PNG Files (*.png)|*.png",
            Multiselect = true,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : null;
    }

    public string? PickFolder(string title = "Chọn thư mục gói pet")
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
