using Tracking_Map_App.Models;
using Tracking_Map_App.PageModels;

namespace Tracking_Map_App.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}