using System;
using System.Windows.Input;

namespace Lan.ImageViewer
{
    // Keeps history optional for host view models implementing the existing public interface.
    internal sealed class DisabledHistoryCommand : ICommand
    {
        internal static readonly DisabledHistoryCommand Instance = new();
        private DisabledHistoryCommand() { }
        public bool CanExecute(object parameter) => false;
        public void Execute(object parameter) { }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
