namespace VChibi.Services;

public interface IFilePickerService
{
    IReadOnlyList<string>? PickPngFiles(string title = "Chọn hình ảnh PNG");
    string? PickFolder(string title = "Chọn thư mục gói pet");
}
