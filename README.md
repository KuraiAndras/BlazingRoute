# BlazingRoute [![Nuget](https://img.shields.io/nuget/v/BlazingRoute)](https://www.nuget.org/packages/BlazingRoute/)

Source generator for generating strongly-typed methods for Blazor routes.

## Code generation

For a project a class is generated with methods to retrieve page routes:

`Artists.razor`:
```razor
@page "/artists/{Id:guid}"

@code {
    [Parameter] public Guid Id { get; set; }
}
```
`Index.razor`:
```razor
@page "/"

<h1>Home</h1>
```

Generates the following `Routes.cs`:

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

## Usage

```razor
@inject NavigationManager Navigation

<NavLink class="nav-link" href="@Routes.Index()">Home Link</NavLink>

<button class="btn btn-primary" @onClick="@Navigate">Some Artist</button>

@code {
    private void Navigate() => Navigation.NavigateTo(Routes.Artists(Guid.NewGuid()));
}
```

## Options

Code generation can be customized. For a project create a file named `RouteGeneration.xml` in the root of the project. In the project `csproj` reference that as an `Additional file`.

```xml
  <ItemGroup>
    <AdditionalFiles Include="RouteGeneration.xml" />
  </ItemGroup>
```

| Name               | Usage                                                                            | Default Value           |
| ------------------ | -------------------------------------------------------------------------------- | ----------------------- |
| ClassName          | Name of the generated class                                                      | Routes                  |
| Namespace          | Namespace of the generated class                                                 | Project's assembly name |
| GenerateExtensions | Generate extensions methods for `NavigationManager`                              | true                    |
| ExtensionPrefix    | Prefix for the extension methods. For example: `NavigateTo` -> `NavigateToIndex` | Empty `string`          |

Sample:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<GenerationOptions>
  <ClassName>MyRoutes</ClassName>
  <Namespace>BlazingRoute.Sample.Routes</Namespace>
  <GenerateExtensions>true</GenerateExtensions>
  <ExtensionPrefix>NavigateTo</ExtensionPrefix>
</GenerationOptions>
```
