using Microsoft.Extensions.DependencyInjection;

namespace Watchtower.Repository.Tests;

// DI-база для тестов (по образцу BaseTatTests из tat.domain).
public class BaseTests
{
    protected ServiceCollection ServiceCollection { get; }
    protected ServiceProvider Sp { get; private set; } = null!;
    private bool _isBuilt;

    protected BaseTests()
    {
        ServiceCollection = new ServiceCollection();
    }

    protected T GetInstance<T>(bool rebuild = false) where T : notnull
    {
        if (rebuild || !_isBuilt)
        {
            Sp = ServiceCollection.BuildServiceProvider();
            _isBuilt = true;
        }

        var instance = Sp.GetService<T>();
        if (instance is null)
            throw new Exception("Error of object initialization using DI");

        return instance;
    }
}
