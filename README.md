# BlazingRoute [![Nuget](https://img.shields.io/nuget/v/BlazingRoute)](https://www.nuget.org/packages/BlazingRoute/)

Source generator for generating strongly-typed methods for Blazor routes.

For a project a class is generated with methods to retrieve page routes:

Artists.razor:

```razor
@page "/artists/{Id:guid}"

@code {
    [Parameter] public Guid Id { get; set; }
}
```

```csharp
namespace Your.ProjectName
{
    public static partial class Routes
    {
        public static ImmutableArray<string> All { get; } = new []
        {
            "/",
            "/artists/{Id:guid}",
        }.ToImmutableArray();

        /// <summary>
        /// /
        /// </summary>
        public static string Index() => $"/";

        /// <summary>
        /// /artists/{Id:guid}
        /// </summary>
        public static string Artists(Guid Id) => $"/artists/{Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
```

# Usage

```razor
@inject NavigationManager Navigation

<NavLink class="nav-link" href="@Routes.Index()">Home Link</NavLink>

<button class="btn btn-primary" @onClick="@Navigate">Some Artist</button>

@code {
    private void Navigate() => Navigation.NavigateTo(Routes.Artists(Guid.NewGuid()));
}
```
