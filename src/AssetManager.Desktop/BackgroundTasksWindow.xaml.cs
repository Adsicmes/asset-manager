using System.Collections.ObjectModel;
using System.Windows;

namespace AssetManager.Desktop;

public partial class BackgroundTasksWindow : Window
{
    private readonly Action<Guid> _requestCancel;

    public BackgroundTasksWindow(
        ObservableCollection<BackgroundTaskRow> tasks,
        Action<Guid> requestCancel)
    {
        Tasks = tasks;
        _requestCancel = requestCancel;

        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<BackgroundTaskRow> Tasks { get; }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BackgroundTaskRow row })
        {
            return;
        }

        _requestCancel(row.Id);
    }
}
