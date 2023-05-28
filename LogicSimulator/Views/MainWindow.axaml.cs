using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using LogicSimulator.Models;
using LogicSimulator.ViewModels;
using LogicSimulator.Views.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicSimulator.Views {
    public partial class MainWindow: Window {
        readonly MainWindowViewModel mwvm;
        readonly Mapper map;

        public MainWindow() {
            Mapper.CreateItemF = CreateItem;
            Mapper.item_types = Enumerable.Range(0, 12).Select(CreateItem).ToArray();
            map = ViewModelBase.map;
            map.Marker_SetState = Marker_SetState;
            map.Marker2_SetState = Marker2_SetState;

            map.SetEllipseMarker = SetEllipseMarker;
            map.SetMarkerColor = SetMarkerColor;
            map.MarkerCenter = MarkerCenter;
            map.MarkerToDistantor = MarkerToDistantor;

            map.JoinPressed = JoinPressed;
            map.JoinMoved = JoinMoved;
            map.JoinReleased = JoinReleased;
            map.JoinTapped = JoinTapped;
            map.CreateJoin = CreateJoin;
            map.UpdateNewJoins = UpdateNewJoins;

            mwvm = new MainWindowViewModel();
            DataContext = mwvm;

            InitializeComponent();
            AddWindow();
        }



        /*
         * Маркеры
         */

        readonly Line marker = new() { Tag = "Marker", ZIndex = 2, IsVisible = false, Stroke = Brushes.YellowGreen, StrokeThickness = 3 };
        readonly Rectangle marker2 = new() { Tag = "Marker", Classes = new("anim"), ZIndex = 2, IsVisible = false, Stroke = Brushes.MediumAquamarine, StrokeThickness = 3 };
        void Marker_SetState(bool? vis, Point? start, Point? end) {
            if (vis != null) marker.IsVisible = (bool) vis;
            if (start != null) marker.StartPoint = (Point) start;
            if (end != null) marker.EndPoint = (Point) end;
        }
        void Marker2_SetState(bool? vis, Thickness? margin, double? w, double? h) {
            if (vis != null) marker2.IsVisible = (bool) vis;
            if (margin != null) {
                var mar = (Thickness) margin;
                double x = mar.Left, y = mar.Top;
                if (x < 0) {
                    if (w != null) w = ((double) w) + x;
                    x = 0;
                }
                if (y < 0) {
                    if (h != null) h = ((double) h) + y;
                    y = 0;
                }
                marker2.Margin = new(x, y, 0, 0);
                if (w != null && h != null) marker2.IsVisible = (double) w > 0 && (double) h > 0;
            }
            if (w != null) marker2.Width = (double) w;
            if (h != null) marker2.Height = (double) h;
        }

        // Дальше идут методы исправления проблем MVVM элитного уровня сложности ;'-}

        Ellipse? last_item;
        Ellipse? marker_A; // Заменяет в картографе marker_circle
        void SetEllipseMarker(bool clear) => marker_A = clear ? null : last_item;
        void SetMarkerColor(ISolidColorBrush fill, ISolidColorBrush stroke) {
            if (marker_A == null) return;
            marker_A.Fill = fill;
            marker_A.Stroke = stroke;
        }
        Point? MarkerCenter() => marker_A?.Center(canv);
        Distantor? MarkerToDistantor() {
            if (marker_A == null) return null;
            var gate = GetGate(marker_A) ?? throw new Exception("Чё?!"); // Такого не бывает
            return gate.GetPin(marker_A);
        }

        /*
         * Фабрикатор IGate
         */

        public static IGate CreateItem(int n) {
            return n switch {
                0 => new AND_2(),
                1 => new OR_2(),
                2 => new NOT(),
                3 => new XOR_2(),
                4 => new PSum(),
                5 => new Switch(),
                6 => new Shapes.Button(),
                7 => new LightBulb(),
                8 => new NAND_2(),
                9 => new FlipFlop(),
                10 => new OR_8(),
                11 => new AND_8(),
                _ => new AND_2(),
            };
        }

        /*
         * Соединения
         */

        Line? old_join, join_line;
        bool join_start;

        string? JoinPressed(Point pos) {
            if (old_join == null) return null;

            JoinedItems.arrow_to_join.TryGetValue(old_join, out var @join2);
            if (@join2 == null) return null;

            /* if (marked_line == @join2) {
                marked_line = null;
                UpdateMarker();
            }*/

            var dist_a = old_join.StartPoint.Hypot(pos);
            var dist_b = old_join.EndPoint.Hypot(pos);
            join_start = dist_a > dist_b;

            Marker_SetState(true,
                join_start ? old_join.StartPoint : pos,
                join_start ? pos : old_join.EndPoint);
            old_join.IsVisible = false;
            return join_start ? @join2.A.tag : @join2.B.tag;
            // marker_mode = CalcMode(join_start ? @join2.A.tag : @join2.B.tag);
        }
        void JoinMoved(Point pos) {
            var p = MarkerCenter() ?? pos;
            Marker_SetState(null, join_start ? null : p, join_start ? p : null);
        }
        void JoinReleased(bool delete_join) {
            if (old_join == null) return;
            JoinedItems.arrow_to_join.TryGetValue(old_join, out var @join);
            var p = MarkerToDistantor();
            if (p != null && @join != null) {
                @join.Delete();

                var newy = join_start ? new JoinedItems(@join.A, p) : new JoinedItems(p, @join.B);
                canv.Children.Add(newy.line);
            } else old_join.IsVisible = true;

            Marker_SetState(false, null, null);
            old_join = null;

            if (delete_join) @join?.Delete();
        }
        JoinedItems? JoinTapped() {
            if (join_line == null) return null;
            if (!JoinedItems.arrow_to_join.TryGetValue(join_line, out var @join)) return null;
            return @join;
        }

        readonly List<JoinedItems> joins = new();
        void CreateJoin(bool add_to_list, Distantor start, Distantor end) {
            var newy = new JoinedItems(start, end);
            canv.Children.Add(newy.line);
            if (add_to_list) joins.Add(newy);
        }
        void UpdateNewJoins() {
            foreach (var join in joins) join.Update();
            joins.Clear();
        }

        /*
         * Привязка управления холстом к обработчикам в картографе
         */

        private static UserControl? GetUC(Control item) {
            while (item.Parent != null) {
                if (item is UserControl @UC) return @UC;
                item = (Control) item.Parent;
            }
            return null;
        }
        private static IGate? GetGate(Control item) {
            var UC = GetUC(item);
            if (UC is IGate @gate) return @gate;
            return null;
        }

        private void FixItem(ref Control? res, Point pos, IEnumerable<ILogical> items) {
            foreach (var logic in items) {
                // if (item.IsPointerOver) { } Гениальная вещь! ;'-} Хотя не, всё равно блокируется после Press и до Release, чего я впринципе хочу избежать ;'-}
                var item = (Control) logic;
                var tb = item.TransformedBounds;
                // if (tb != null && new Rect(tb.Value.Clip.TopLeft, new Size()).Sum(item.Bounds).Contains(pos) && (string?) item.Tag != "Join") res = item; // Гениально! ;'-} НАКОНЕЦ-ТО ЗАРАБОТАЛО! (Так было в 8 лабе)
                if (tb != null && tb.Value.Bounds.TransformToAABB(tb.Value.Transform).Contains(pos) && (string?) item.Tag != "Join") res = item; // Гениально! Апгрейд прошёл успешно :D
                FixItem(ref res, pos, item.GetLogicalChildren());
            }
        }
        private Control FixItem(Control old, Canvas canv, Point pos) {
            int mode = ViewModelBase.map.GetMode();
            Control? item = null;
            if (mode != 5 && mode != 6 && mode != 7 && mode != 8) return old;

            var tb = canv.TransformedBounds;
            if (tb != null) {
                var bounds = tb.Value.Bounds.TransformToAABB(tb.Value.Transform);
                FixItem(ref item, pos + bounds.TopLeft, canv.Children);
                // Log.Write("tag: " + item.Tag);
            }
            return item ?? new Canvas() { Tag = "Scene" };
        }

        private Canvas canv = new();
        public void AddWindow() {
            canv = this.Find<Canvas>("Canvas");
            var map = ViewModelBase.map;

            if (canv == null) return; // Такого не бывает

            canv.Children.Add(marker);
            canv.Children.Add(marker2);

            var panel = (Panel?) canv.Parent;
            if (panel == null) return; // Такого не бывает

            panel.PointerPressed += (object? sender, PointerPressedEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) {
                    last_item = @control is Ellipse @ellipse ? @ellipse : null;
                    old_join = join_line = @control is Line @line ? @line : null;
                    map.Press(GetGate(@control), (string?) @control.Tag, e.GetCurrentPoint(canv).Position);
                }
            };
            panel.PointerMoved += (object? sender, PointerEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) {
                    var pos = e.GetCurrentPoint(canv).Position;
                    var item = FixItem(@control, canv, pos);
                    last_item = item is Ellipse @ellipse ? @ellipse : null;
                    map.Move(GetGate(item), (string?) item.Tag, pos);
                }
            };
            panel.PointerReleased += (object? sender, PointerReleasedEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) {
                    var pos = e.GetCurrentPoint(canv).Position;
                    var item = FixItem(@control, canv, pos);
                    last_item = item is Ellipse @ellipse ? @ellipse : null;
                    int mode = map.Release(GetGate(item), (string?) item.Tag, pos);

                    bool tap = map.tapped;
                    if (tap && mode == 1) {
                        if (canv == null) return; // Такого не бывает

                        var newy = map.GenSelectedItem();
                        newy.Move(map.tap_pos);
                        map.AddItem(newy);
                    }
                }
            };
            panel.PointerWheelChanged += (object? sender, PointerWheelEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.WheelMove(GetGate(@control), (string?) @control.Tag, e.Delta.Y, e.GetCurrentPoint(canv).Position);
            };
            KeyDown += (object? sender, KeyEventArgs e) => {
                if (e.Source != null && e.Source is Control @control) map.KeyPressed(e.Key);
            };

            mwvm.CommUsed += FuncComm;

            map.AddItemToCanvas += (IGate gate) => {
                if (gate is UserControl @UC) canv.Children.Add(@UC);
            };
        }

        /*
         * Обработка переименовывателя схем и проектов
         */

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

        /*
         * Остальное
         */

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