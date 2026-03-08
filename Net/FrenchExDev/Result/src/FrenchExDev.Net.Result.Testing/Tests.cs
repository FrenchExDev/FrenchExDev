using FrenchExDev.Net.Result;
namespace FrenchExDev.Net.Result.Testing;

public class MyClass
{
    public Result<MyClass, MyException> DoSomething(bool succeed)
    {
        if (succeed)
        {
            return Result<MyClass, MyException>.Success(this);
        }
        else
        {
            return Result<MyClass, MyException>.Failure(new MyException("Something went wrong"));
        }
    }
}

public class MyException : Exception
{
    public MyException(string message) : base(message) { }
}
