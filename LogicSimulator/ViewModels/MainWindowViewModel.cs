using LogicSimulator.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;

namespace LogicSimulator.ViewModels {
    public class MainWindowViewModel: ViewModelBase {
        private string log = "";
        public string Logg { get => log; set => this.RaiseAndSetIfChanged(ref log, value); }

        static readonly List<string> logs = new();
        private void LogHandler(string message) {
            foreach (var mess in message.Split('\n')) logs.Add(mess);
            while (logs.Count > 45) logs.RemoveAt(0);
            Logg = string.Join('\n', logs);
        }



        public MainWindowViewModel() { // Если я буду Window mw передавать через этот конструктор, то предварительный просмотр снова порвёт смачно XD
            Log.NewLine += LogHandler;
            Comm = ReactiveCommand.Create<string, Unit>(n => { FuncComm(n); return new Unit(); });
            NewItem = ReactiveCommand.Create<Unit, Unit>(_ => { FuncNewItem(); return new Unit(); });
            ResizerOption = ReactiveCommand.Create<Unit, Unit>(_ => { FuncResizerOption?.Invoke(); return new Unit(); });
            SimulateOption = ReactiveCommand.Create<Unit, Unit>(_ => { FuncSimulateOption?.Invoke(); return new Unit(); });
        }

        public static IGate[] ItemTypes { get => Mapper.item_types; }
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
        public ReactiveCommand<string, Unit> Comm { get; }

        private static void FuncNewItem() {
            CurrentProj?.AddScheme(null);
        }
        public ReactiveCommand<Unit, Unit> NewItem { get; }

        public static Project? GetCurProject => CurrentProj;
        public static bool LockSelfConnect { get => map.lock_self_connect; set => map.lock_self_connect = value; }

        public Action? FuncResizerOption;
        public ReactiveCommand<Unit, Unit> ResizerOption { get; }

        public Action? FuncSimulateOption;
        public ReactiveCommand<Unit, Unit> SimulateOption { get; }
    }
}