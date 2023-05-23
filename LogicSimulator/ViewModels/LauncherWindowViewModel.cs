using ReactiveUI;
using System.Reactive;
using LogicSimulator.Models;

namespace LogicSimulator.ViewModels {
    public class LauncherWindowViewModel: ViewModelBase {
        public LauncherWindowViewModel() {
            Create = ReactiveCommand.Create<Unit, Unit>(_ => { FuncCreate(); return new Unit(); });
            Open = ReactiveCommand.Create<Unit, Unit>(_ => { FuncOpen(); return new Unit(); });
            Exit = ReactiveCommand.Create<Unit, Unit>(_ => { FuncExit(); return new Unit(); });
        }

        public delegate void CommHandler(string comm);
        public event CommHandler? CommUsed;

        void FuncCreate() => CommUsed?.Invoke("Create");
        void FuncOpen() => CommUsed?.Invoke("Open");
        void FuncExit() => CommUsed?.Invoke("Exit");

        public ReactiveCommand<Unit, Unit> Create { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Exit { get; }


        public static Project[] ProjectList { get => map.filer.GetSortedProjects(); }
    }
}