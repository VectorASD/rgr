using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using LogicSimulator.Models;
using LogicSimulator.ViewModels;

namespace LogicSimulator.Views {
    public partial class MainWindow: Window {
        readonly MainWindowViewModel mwvm;

        public MainWindow() {
            InitializeComponent();
            mwvm = new MainWindowViewModel();
            DataContext = mwvm;
            AddWindow();
        }

        public void AddWindow() {
            var canv = this.Find<Canvas>("Canvas");
            var map = ViewModelBase.map;

            map.canv = canv;
            if (canv == null) return; // Такого не бывает

            canv.Children.Add(map.Marker);
            canv.Children.Add(map.Marker2);

            var panel = (Panel?) canv.Parent;
            if (panel == null) return; // Такого не бывает

            panel.PointerPressed += (object? sender, PointerPressedEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.Press(@control, e.GetCurrentPoint(canv).Position);
            };
            panel.PointerMoved += (object? sender, PointerEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.Move(@control, e.GetCurrentPoint(canv).Position);
            };
            panel.PointerReleased += (object? sender, PointerReleasedEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) {
                    int mode = map.Release(@control, e.GetCurrentPoint(canv).Position);
                    bool tap = map.tapped;
                    if (tap && mode == 1) {
                        var pos = map.tap_pos;
                        if (canv == null) return; // Такого не бывает

                        var newy = map.GenSelectedItem();
                        newy.Move(pos);
                        map.AddItem(newy);
                    }
                }
            };
            panel.PointerWheelChanged += (object? sender, PointerWheelEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.WheelMove(@control, e.Delta.Y, e.GetCurrentPoint(canv).Position);
            };
            KeyDown += (object? sender, KeyEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.KeyPressed(@control, e.Key);
            };

            mwvm.CommUsed += FuncComm;
        }

        Grid? cur_grid;
        TextBlock? old_b_child;
        object? old_b_child_tag;
        string? prev_scheme_name;

        public void DTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
            var src = (Control?) e.Source;

            if (src is ContentPresenter cp && cp.Child is Border bord) src = bord;
            if (src is Border bord2 && bord2.Child is Grid g2) src = g2;
            if (src is Grid g3 && g3.Children[0] is TextBlock tb2) src = tb2;

            if (src is not TextBlock tb) return;

            var p = tb.Parent;
            if (p == null) return;

            if (old_b_child != null)
                if (cur_grid != null) cur_grid.Children[0] = old_b_child;

            if (p is not Grid g) return;
            cur_grid = g;

            old_b_child = tb;
            old_b_child_tag = tb.Tag;
            prev_scheme_name = tb.Text;

            var newy = new TextBox { Text = tb.Text }; // Изи блиц-транcформация в одну строчку ;'-}

            // Log.Write("Tag: " + tb.Tag);
            cur_grid.Children[0] = newy;
            //Log.Write("Tag: " + tb.Tag); // КААААК?!?!?!? Почему пропажа предка удаляет Tag?!

            newy.KeyUp += (object? sender, KeyEventArgs e) => {
                if (e.Key != Key.Return) return;

                if (newy.Text != prev_scheme_name) {
                    // tb.Text = newy.Text;
                    if ((string?) tb.Tag == "p_name") MainWindowViewModel.GetCurProject?.ChangeName(newy.Text);
                    else if (old_b_child_tag is Scheme scheme) scheme.ChangeName(newy.Text);
                }

                cur_grid.Children[0] = tb;
                cur_grid = null; old_b_child = null;
            };
        }

        public void Update() {
            Log.Write("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n    Текущий проект:\n" + ViewModelBase.CurrentProj);

            ViewModelBase.map.ImportScheme();
            mwvm.Update();
            Width++; // ГОРАААААААААААААЗДО больше толку, чем от всех этих НЕРАБОЧИХ через раз RaisePropertyChanged
        }

        public void FuncComm(string Comm) {
            var map = ViewModelBase.map;

            switch (Comm) {
            case "Create":
                var newy = map.filer.CreateProject();
                ViewModelBase.CurrentProj = newy;
                Update();
                break;
            case "Open":
                var selected = map.filer.SelectProjectFile(this);
                if (selected != null) {
                    ViewModelBase.CurrentProj = selected;
                    Update();
                }
                break;
            case "Save":
                map.Export();
                // Для создания тестовых штучек:
                // File.WriteAllText("../../../for_test.json", Utils.Obj2json((map.current_scheme ?? throw new System.Exception("Чё?!")).Export()));
                break;
            case "SaveAs":
                map.Export();
                ViewModelBase.CurrentProj?.SaveAs(this);
                // this.RaisePropertyChanged(new(nameof(CanSave)));
                break;
            case "ExitToLauncher":
                new LauncherWindow().Show();
                Hide();
                break;
            case "Exit":
                Close();
                break;
            }
        }
    }
}