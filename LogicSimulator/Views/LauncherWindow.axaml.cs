using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using LogicSimulator.Models;
using LogicSimulator.ViewModels;

namespace LogicSimulator.Views {
    public partial class LauncherWindow: Window {
        readonly LauncherWindowViewModel lwvm;
        private static readonly MainWindow mw = new();

        public LauncherWindow() {
            InitializeComponent();
            lwvm = new LauncherWindowViewModel();
            DataContext = lwvm;

            lwvm.CommUsed += FuncComm;
        }

        public void DTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
            var src = (Control?) e.Source;

            if (src is ContentPresenter cp && cp.Child is Border bord) src = bord;
            if (src is Border bord2 && bord2.Child is TextBlock tb2) src = tb2;

            if (src is not TextBlock tb || tb.Tag is not Project proj) return;

            ViewModelBase.CurrentProj = proj;

            mw.Show();
            mw.Update();
            Close();
        }

        void FuncComm(string comm) {
            var map = ViewModelBase.map;

            switch (comm) {
            case "Create":
                var newy = map.filer.CreateProject();
                ViewModelBase.CurrentProj = newy;
                mw.Show();
                mw.Update();
                Close();
                break;
            case "Open":
                var selected = map.filer.SelectProjectFile(this);
                if (selected == null) return;

                ViewModelBase.CurrentProj = selected;
                mw.Show();
                mw.Update();
                Close();
                break;
            case "Exit":
                Close();
                mw.Close();
                break;
            }
        }

        /*
         * Для тестирования
         */

        public static MainWindow GetMW => mw;
    }
}