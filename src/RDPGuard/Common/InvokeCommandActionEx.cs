using Microsoft.Xaml.Behaviors;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace RDPGuard.Common
{
    /// <summary>
    /// 扩展的 InvokeCommandAction
    /// </summary>
    public class InvokeCommandActionEx : TriggerAction<DependencyObject>
    {
        private string? commandName;

        public string? CommandName
        {
            get
            {
                ReadPreamble();
                return commandName;
            }
            set
            {
                if (commandName != value)
                {
                    WritePreamble();
                    commandName = value;
                    WritePostscript();
                }
            }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(InvokeCommandActionEx), null);

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(InvokeCommandActionEx), null);

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        protected override void Invoke(object? parameter)
        {
            if (AssociatedObject != null)
            {
                var command = ResolveCommand();
                var exParameter = new CommandParameterEx
                {
                    Sender = AssociatedObject,
                    Parameter = GetValue(CommandParameterProperty),
                    EventArgs = parameter as EventArgs
                };

                if (command != null && command.CanExecute(exParameter))
                {
                    command.Execute(exParameter);
                }
            }
        }

        private ICommand? ResolveCommand()
        {
            if (Command != null) return Command;
            if (AssociatedObject != null && !string.IsNullOrEmpty(CommandName))
            {
                var type = AssociatedObject.GetType();
                var prop = type.GetProperty(CommandName, BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && typeof(ICommand).IsAssignableFrom(prop.PropertyType))
                {
                    return (ICommand?)prop.GetValue(AssociatedObject, null);
                }
            }
            return null;
        }
    }
}
