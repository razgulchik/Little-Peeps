namespace LittlePeeps
{
    public interface ICommand
    {
        bool CanExecute();
        void Execute();
    }
}
