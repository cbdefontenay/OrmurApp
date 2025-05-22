using Color = Microsoft.Maui.Graphics.Color;

namespace Ormur;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        
        StatusBarBehaviorName.StatusBarColor = Color.FromRgb(216, 226, 255);
    }
}