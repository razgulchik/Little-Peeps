public interface ICommand
{
    bool CanExecute();
    void Execute();
}
