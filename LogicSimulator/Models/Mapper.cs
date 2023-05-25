using Avalonia;
using System;
using System.Collections.Generic;
using DynamicData;
using Avalonia.Media;
using System.Linq;
using Avalonia.Input;

namespace LogicSimulator.Models {
    public class Mapper {
        public Action<bool?, Point?, Point?>? Marker_SetState; // IsVisible | StartPoint | EndPoint
        public Action<bool?, Thickness?, double?, double?>? Marker2_SetState; // IsVisible | Margin | Width | Height 

        public readonly Simulator sim = new(); // забавно, но без public рефлексия вообще не видит этот параметр, от чего ER-diagram_exTRACTOR теряет одну стрелочку зависимости...

        /*
         * Маркер
         */

        private IGate? marked_item;
        private JoinedItems? marked_line;

        private void UpdateMarker() {
            bool visible = marked_item != null || marked_line != null;
            Marker2_SetState?.Invoke(visible, null, null, null);

            if (marked_item != null) {
                var bound = marked_item.GetBounds();
                Marker2_SetState?.Invoke(null, new(bound.X, bound.Y), bound.Width, bound.Height);
                marked_line = null;
            }

            if (marked_line != null) {
                var line = marked_line.line;
                var A = line.StartPoint;
                var B = line.EndPoint;
                var pos = new Thickness(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y));
                Marker2_SetState?.Invoke(null, pos, Math.Abs(A.X - B.X), Math.Abs(A.Y - B.Y));
            }
        }

        /*
         * Выборка элементов
         */

        private int selected_item = 0;
        public int SelectedItem { get => selected_item; set => selected_item = value; }

        public static Func<int, IGate>? CreateItemF;
        private static IGate CreateItem(int n) {
            if (CreateItemF == null) throw new Exception("Не определён фабрикатор IGate извне картографа");
            return CreateItemF(n);
        }

        public static IGate[] item_types = Array.Empty<IGate>();

        public IGate GenSelectedItem() => CreateItem(selected_item);

        /*
         * Хранилище
         */

        readonly List<IGate> items = new();

        public delegate void AddHandler(IGate gate);
        public event AddHandler? AddItemToCanvas;

        public void AddItem(IGate item) {
            items.Add(item);
            sim.AddItem(item);
            AddItemToCanvas?.Invoke(item);
        }
        public void RemoveItem(IGate item) {
            if (marked_item != null) {
                marked_item = null;
                UpdateMarker();
            }
            if (marked_line != null && item.ContainsJoin(marked_line)) {
                marked_line = null;
                UpdateMarker();
            }

            items.Remove(item);
            sim.RemoveItem(item);

            item.ClearJoins();
        }
        public void RemoveAll() {
            foreach (var item in items.ToArray()) RemoveItem(item);
            sim.Clear();
        }

        private void SaveAllPoses() {
            foreach (var item in items) item.SavePose();
        }

        /*
         * Определение режима перемещения
         */

        int mode = 0;
        /*
         *    Режимы:
         * 0 - ничего не делает
         * 1 - двигаем камеру
         * 2 - двигаем элемент
         * 3 - тянем элемент
         * 4 - вышвыриваем элемент
         * 5 - тянем линию от входа (In)
         * 6 - тянем линию от выхода (Out)
         * 7 - тянем линию от узла (IO)
         * 8 - тянем уже существующее соединение - переподключаем
        */

        private static int CalcMode(string? tag) {
            if (tag == null) return 0;
            return tag switch {
                "Scene" => 1,
                "Body" => 2,
                "Resizer" => 3,
                "Deleter" => 4,
                "In" => 5,
                "Out" => 6,
                "IO" => 7,
                "Join" => 8,
                "Pin" or _ => 0,
            };
        }

        private static bool IsMode(string? tag, string[] mods) {
            if (tag == null) return false;
            return mods.IndexOf(tag) != -1;
        }

        /*
         * Обработка мыши
         */

        Point moved_pos;
        IGate? moved_item;
        Point item_old_pos;
        Size item_old_size;

        Distantor? start_dist;
        int marker_mode;

        public Func<Point, string?>? JoinPressed;
        public Action<Point>? JoinMoved;
        public Action<bool>? JoinReleased;
        public Func<JoinedItems?>? JoinTapped;
        public Action<bool, Distantor, Distantor>? CreateJoin;
        public Action? UpdateNewJoins;

        bool delete_join = false;

        public bool lock_self_connect = true;

        public void Press(IGate? item, string? tag, Point pos) {
            // Log.Write("PointerPressed: " + item.GetType().Name + " pos: " + pos);
            
            mode = CalcMode(tag);
            // Log.Write("new_mode: " + mode);

            moved_pos = pos;
            moved_item = item;
            tapped = true;
            if (moved_item != null) item_old_pos = moved_item.GetPos();

            switch (mode) {
            case 1:
                SaveAllPoses();
                break;
            case 3:
                if (moved_item == null) break;
                item_old_size = moved_item.GetBodySize();
                break;
            case 5 or 6 or 7:
                start_dist = MarkerToDistantor?.Invoke();
                if (start_dist == null) break;

                var circle_pos = start_dist.GetPos();
                Marker_SetState?.Invoke(true, circle_pos, circle_pos);
                marker_mode = mode;
                break;
            case 8:
                var tag2 = JoinPressed?.Invoke(pos);
                marker_mode = CalcMode(tag2);
                break;
            }

            Move(item, tag, pos);
        }



        public Action<bool>? SetEllipseMarker;
        public Action<ISolidColorBrush, ISolidColorBrush>? SetMarkerColor;
        public Func<Point?>? MarkerCenter;
        public Func<Distantor?>? MarkerToDistantor;

        private readonly ISolidColorBrush transparent = new SolidColorBrush(Color.Parse("#0000"));

        public int GetMode() => mode;
        public void Move(IGate? item, string? tag, Point pos) {
            // Log.Write("PointerMoved: " + item.GetType().Name + " pos: " + pos);

            string[] mods = new[] { "In", "Out", "IO" };
            if (IsMode(tag, mods)
                && !(marker_mode == 5 && tag == "In" || marker_mode == 6 && tag == "Out" ||
                lock_self_connect && moved_item == item)) { // То самое место, что не даёт подключить вход ко входу, либо выход к выходу

                SetMarkerColor?.Invoke(transparent, Brushes.Gray);
                SetEllipseMarker?.Invoke(false);
                SetMarkerColor?.Invoke(Brushes.Lime, Brushes.Green);
            } else {
                SetMarkerColor?.Invoke(transparent, Brushes.Gray);
                SetEllipseMarker?.Invoke(true);
            }

            if (mode == 8) delete_join = tag == "Deleter";

            /* if (mode == 0 && (string?) item.Tag == "Join") { DEBUG
                JoinedItems.arrow_to_join.TryGetValue((Line) item, out var @join);
                if (@join != null) Log.Write("J a->b: id" + items.IndexOf(@join.A.parent) + " n:" + @join.A.num + "    id" + items.IndexOf(@join.B.parent) + " n:" + @join.B.num);
            }*/



            var delta = pos - moved_pos;
            if (delta.X == 0 && delta.Y == 0) return;

            if (Math.Pow(delta.X, 2) + Math.Pow(delta.Y, 2) > 9) tapped = false;

            switch (mode) {
            case 1:
                foreach (var item_ in items) {
                    var pose = item_.GetPose();
                    item_.Move(pose + delta, true);
                }
                UpdateMarker();
                break;
            case 2:
                if (moved_item == null) break;
                var new_pos = item_old_pos + delta;
                moved_item.Move(new_pos);
                UpdateMarker();
                break;
            case 3:
                if (moved_item == null) break;
                var new_size = item_old_size + new Size(delta.X, delta.Y);
                moved_item.Resize(new_size);
                UpdateMarker();
                break;
            case 5 or 6 or 7:
                var end_pos = MarkerCenter?.Invoke() ?? pos;
                Marker_SetState?.Invoke(null, null, end_pos);
                break;
            case 8:
                JoinMoved?.Invoke(pos);
                break;
            }
        }

        public bool tapped = false; // Обрабатывается после Release
        public Point tap_pos; // Обрабатывается после Release

        public int Release(IGate? item, string? tag, Point pos) {
            Move(item, tag, pos);
            // Log.Write("PointerReleased: " + item.GetType().Name + " pos: " + pos);

            switch (mode) {
            case 5 or 6 or 7:
                if (start_dist == null) break;
                var end_dist = MarkerToDistantor?.Invoke();
                if (end_dist != null) {
                    // Log.Write("Стартовый элемент: " + start_dist.parent + " (" + start_dist.GetPos() + ")");
                    // Log.Write("Конечный  элемент: " + end_dist.parent   + " (" + end_dist.GetPos()   + ")");
                    CreateJoin?.Invoke(false, start_dist, end_dist);
                }
                Marker_SetState?.Invoke(false, null, null);
                marker_mode = 0;
                break;
            case 8:
                JoinReleased?.Invoke(delete_join);
                marker_mode = 0;
                delete_join = false;
                break;
            }

            if (tapped) Tapped(item, pos);

            int res_mode = mode;
            mode = 0;
            moved_item = null;
            return res_mode;
        }

        private void Tapped(IGate? _, Point pos) {
            // Log.Write("Tapped: " + item.GetType().Name + " pos: " + pos + " mode: " + mode);
            tap_pos = pos;

            switch (mode) {
            /* case 4:
                if (moved_item != null) RemoveItem(moved_item);
                break; */
            case 2 or 8:
                var join = JoinTapped?.Invoke();
                if (join != null) {
                    marked_item = null;
                    marked_line = join;
                    UpdateMarker();
                    break;
                }

                if (moved_item == null) break;

                marked_item = moved_item;
                UpdateMarker();
                break;
            }
        }

        public void WheelMove(IGate? item, string? tag, double move, Point pos) {
            // Log.Write("WheelMoved: " + item.GetType().Name + " delta: " + (move > 0 ? 1 : -1));
            int mode = CalcMode(tag);
            double scale = move > 0 ? 1.1 : 1 / 1.1;
            double inv_scale = 1 / scale;

            switch (mode) {
            case 1:
                foreach (var gate in items) {
                    gate.ChangeScale(scale, true);

                    var item_pos = gate.GetPos();
                    var delta = item_pos - pos;
                    delta *= scale;
                    var new_pos = delta + pos;
                    gate.Move(new_pos, false);
                }
                UpdateMarker();
                break;
            case 2:
                if (item == null) return;
                item.ChangeScale(inv_scale);
                UpdateMarker();
                break;
            }
        }

        public void KeyPressed(Key key) {
            // Log.Write("KeyPressed: " + item.GetType().Name + " key: " + key);
            switch (key) {
            case Key.Up:
            case Key.Left:
            case Key.Right:
            case Key.Down:
                int dx = key == Key.Left ? -1 : key == Key.Right ? 1 : 0;
                int dy = key == Key.Up ? -1 : key == Key.Down ? 1 : 0;
                marked_item?.Move(marked_item.GetPos() + new Point(dx * 10, dy * 10));
                UpdateMarker();
                break;
            case Key.Delete:
                if (marked_item != null) RemoveItem(marked_item);
                if (marked_line != null) {
                    marked_line.Delete();
                    marked_line = null;
                    UpdateMarker();
                }
                break;
            }
        }


        /*
         * Экспорт и импорт
         */

        public readonly FileHandler filer = new();
        public Scheme? current_scheme;

        public void Export() {
            if (current_scheme == null) return;

            var arr = items.Select(x => x.Export()).ToArray();

            Dictionary<IGate, int> item_to_num = new();
            int n = 0;
            foreach (var item in items) item_to_num.Add(item, n++);
            List<object[]> joins = new();
            foreach (var item in items) joins.Add(item.ExportJoins(item_to_num));

            sim.Clean();
            string states = sim.Export();

            try { current_scheme.Update(arr, joins.ToArray(), states); }
            catch (Exception e) { Log.Write("Save error:\n" + e); }

            /* Log.Write("Items: " + Utils.Obj2json(arr));
            Log.Write("Joins: " + Utils.Obj2json(joins));
            Log.Write("States: " + Utils.Obj2json(states)); */
        }

        public void ImportScheme(bool start = true) {
            if (current_scheme == null) return;

            sim.Stop();
            sim.lock_sim = true;

            RemoveAll();

            List<IGate> list = new();
            foreach (var item in current_scheme.items) {
                if (item is not Dictionary<string, object> @dict) { Log.Write("Не верный тип элемента: " + item); continue; }

                if (!@dict.TryGetValue("id", out var @value)) { Log.Write("id элемента не обнаружен"); continue; }
                if (@value is not int @id) { Log.Write("Неверный тип id: " + @value); continue; }
                var newy = CreateItem(@id);

                newy.Import(@dict);
                AddItem(newy);
                list.Add(newy);
            }
            var items_arr = list.ToArray();

            foreach (var obj in current_scheme.joins) {
                object[] join;
                if (obj is List<object> @j) join = @j.ToArray();
                else if (obj is object[] @j2) join = @j2;
                else { Log.Write("Одно из соединений не того типа: " + obj + " " + Utils.Obj2json(obj)); continue; }
                if (join.Length != 6 ||
                    join[0] is not int @num_a || join[1] is not int @pin_a || join[2] is not string @tag_a ||
                    join[3] is not int @num_b || join[4] is not int @pin_b || join[5] is not string @tag_b) { Log.Write("Содержимое списка соединения ошибочно"); continue; }

                CreateJoin?.Invoke(true, new(items_arr[@num_a], @pin_a, tag_a), new(items_arr[@num_b], @pin_b, tag_b));
            }
            UpdateNewJoins?.Invoke();

            sim.Import(current_scheme.states);
            sim.lock_sim = false;
            if (start) sim.Start(); // Во время тестирования лучше и близко не прикасаться к этой функции XD
        }
    }
}
