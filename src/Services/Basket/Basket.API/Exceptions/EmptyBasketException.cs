namespace Basket.API.Exceptions;

public class EmptyBasketException : BadRequestException
{
    public EmptyBasketException(string userName)
        : base($"Basket of {userName} is empty and cannot be checked out.")
    {
    }
}
