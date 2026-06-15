using System;

public class ReactiveValue<T>
{
    private T value;

    public event Action<T> OnChanged;

    public T Value
    {
        get => value;
        set
        {
            this.value = value;
            OnChanged?.Invoke(value);
        }
    }

    public ReactiveValue(T initialValue)
    {
        value = initialValue;
    }
}
