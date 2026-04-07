namespace Walos.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string resource = "Recurso")
        : base($"{resource} no encontrado")
    {
    }
}
