using LogicSimulator.Models;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;

namespace LogicSimulator.ViewModels
{
    public class Log {
        static readonly List<string> logs = new();
        static readonly string path = "../../../Log.txt";
        static bool first = true;

        static readonly bool use_file = false;

        public static MainWindowViewModel? Mwvm { private get; set; }
        public static void Write(string message, bool without_update = false) {
            if (!without_update) {
                foreach (var mess in message.Split('\n')) logs.Add(mess);
                while (logs.Count > 45) logs.RemoveAt(0);

                if (Mwvm != null) Mwvm.Logg = string.Join('\n', logs);
            }

            if (use_file) {
                if (first) File.WriteAllText(path, message + "\n");
                else File.AppendAllText(path, message + "\n");
                first = false;
            }
        }
    }

    public class MainWindowViewModel: ViewModelBase {
        private string log = "";
        public string Logg { get => log; set => this.RaiseAndSetIfChanged(ref log, value); }

        public MainWindowViewModel() { // Если я буду Window mw передавать через этот конструктор, то предварительный просмотр снова порвёт смачно XD
            Log.Mwvm = this;
            Comm = ReactiveCommand.Create<string, Unit>(n => { FuncComm(n); return new Unit(); });
            NewItem = ReactiveCommand.Create<Unit, Unit>(_ => { FuncNewItem(); return new Unit(); });
        }

        public static IGate[] ItemTypes { get => map.item_types; }
        public static int SelectedItem { get => map.SelectedItem; set => map.SelectedItem = value; }

        /*
         * Обработка той самой панели со схемами проекта
         */

        public static string ProjName { get => CurrentProj == null ? "???" : CurrentProj.Name; }

        public static ObservableCollection<Scheme> Schemes { get => CurrentProj == null ? new() : CurrentProj.schemes; }



        public void Update() {
            this.RaisePropertyChanged(new(nameof(ProjName)));
            this.RaisePropertyChanged(new(nameof(Schemes)));
            this.RaisePropertyChanged(new(nameof(CanSave)));
        }

        public static bool CanSave { get => CurrentProj != null && CurrentProj.CanSave(); }

        /*
         * Кнопочки!
         */

        public delegate void CommHandler(string comm);
        public event CommHandler? CommUsed;

        public void FuncComm(string Comm) {
            CommUsed?.Invoke(Comm);
        }

        public static Project? GetCurProject => CurrentProj;

        public ReactiveCommand<string, Unit> Comm { get; }

        private static void FuncNewItem() {
            CurrentProj?.AddScheme(null);
        }

        public ReactiveCommand<Unit, Unit> NewItem { get; }

        public static bool LockSelfConnect { get => map.lock_self_connect; set => map.lock_self_connect = value; }
    }
}