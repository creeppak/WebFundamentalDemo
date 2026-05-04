using MudBlazor;

namespace Web.Layout;

public static class AppTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            // Brand
            Primary             = "#000",
            PrimaryContrastText = "#FFFFFF",
            Secondary           = "#03a300",
            SecondaryContrastText = "#FFFFFF",
            Tertiary            = "#1EC8A5",
            TertiaryContrastText = "#FFFFFF",

            // Semantic
            Info    = "#2196F3",
            Success = "#03a300",
            Warning = "#FF9800",
            Error   = "#F44336",
            Dark    = "#424242",

            // Surfaces & backgrounds
            Background       = "#FFFFFF",
            BackgroundGray   = "#F5F5F5",
            Surface          = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            AppbarBackground = "#000",

            // Text
            TextPrimary   = "#424242",
            TextSecondary = "rgba(0,0,0,0.54)",
            TextDisabled  = "rgba(0,0,0,0.38)",
            DrawerText    = "#424242",
            DrawerIcon    = "#616161",
            AppbarText    = "#FFFFFF",

            // Actions
            ActionDefault            = "rgba(0,0,0,0.54)",
            ActionDisabled           = "rgba(0,0,0,0.26)",
            ActionDisabledBackground = "rgba(0,0,0,0.12)",

            // Lines, dividers & tables
            LinesDefault = "rgba(0,0,0,0.12)",
            LinesInputs  = "#BDBDBD",
            Divider      = "#E0E0E0",
            TableLines   = "#E0E0E0",
            TableHover   = "rgba(0,0,0,0.04)",
            TableStriped = "rgba(0,0,0,0.02)",
        },

        PaletteDark = new PaletteDark
        {
            // Brand
            Primary             = "#42bf2b",
            PrimaryContrastText = "#FFFFFF",
            Secondary           = "#44C82D",
            SecondaryContrastText = "#FFFFFF",
            Tertiary            = "#1EC8A5",
            TertiaryContrastText = "#FFFFFF",

            // Semantic
            Info    = "#3299FF",
            Success = "#4BD133",
            Warning = "#FFA800",
            Error   = "#F64E62",
            Dark    = "#27272F",

            // Surfaces & backgrounds
            Background       = "#32333D",
            BackgroundGray   = "#27272F",
            Surface          = "#373740",
            DrawerBackground = "#27272F",
            AppbarBackground = "#27272F",

            // Text
            TextPrimary   = "rgba(255,255,255,0.70)",
            TextSecondary = "rgba(255,255,255,0.50)",
            TextDisabled  = "rgba(255,255,255,0.20)",
            DrawerText    = "rgba(255,255,255,0.70)",
            DrawerIcon    = "rgba(255,255,255,0.70)",
            AppbarText    = "rgba(255,255,255,1.00)",

            // Actions
            ActionDefault            = "#ADADB1",
            ActionDisabled           = "rgba(255,255,255,0.26)",
            ActionDisabledBackground = "rgba(255,255,255,0.12)",

            // Lines, dividers & tables
            LinesDefault = "rgba(255,255,255,0.12)",
            LinesInputs  = "rgba(255,255,255,0.30)",
            Divider      = "rgba(255,255,255,0.12)",
            DividerLight = "rgba(255,255,255,0.06)",
            TableLines   = "rgba(255,255,255,0.12)",
            TableStriped = "rgba(255,255,255,0.20)",
        },

        Typography = new Typography
        {
            Default   = new DefaultTypography   { FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".875rem", FontWeight = "400", LineHeight = "1.43",  LetterSpacing = ".01071em" },
            H1        = new H1Typography        { FontSize = "6rem",     FontWeight = "300", LineHeight = "1.167", LetterSpacing = "-.01562em" },
            H2        = new H2Typography        { FontSize = "3.75rem",  FontWeight = "300", LineHeight = "1.2",   LetterSpacing = "-.00833em" },
            H3        = new H3Typography        { FontSize = "3rem",     FontWeight = "400", LineHeight = "1.167", LetterSpacing = "0" },
            H4        = new H4Typography        { FontSize = "2.125rem", FontWeight = "400", LineHeight = "1.235", LetterSpacing = ".00735em" },
            H5        = new H5Typography        { FontSize = "1.5rem",   FontWeight = "400", LineHeight = "1.334", LetterSpacing = "0" },
            H6        = new H6Typography        { FontSize = "1.25rem",  FontWeight = "500", LineHeight = "1.6",   LetterSpacing = ".0075em" },
            Subtitle1 = new Subtitle1Typography { FontSize = "1rem",     FontWeight = "400", LineHeight = "1.75",  LetterSpacing = ".00938em" },
            Subtitle2 = new Subtitle2Typography { FontSize = ".875rem",  FontWeight = "500", LineHeight = "1.57",  LetterSpacing = ".00714em" },
            Body1     = new Body1Typography     { FontSize = "1rem",     FontWeight = "400", LineHeight = "1.5",   LetterSpacing = ".00938em" },
            Body2     = new Body2Typography     { FontSize = ".875rem",  FontWeight = "400", LineHeight = "1.43",  LetterSpacing = ".01071em" },
            Button    = new ButtonTypography    { FontSize = ".875rem",  FontWeight = "500", LineHeight = "1.75",  LetterSpacing = ".02857em", TextTransform = "uppercase" },
            Caption   = new CaptionTypography   { FontSize = ".75rem",   FontWeight = "400", LineHeight = "1.66",  LetterSpacing = ".03333em" },
            Overline  = new OverlineTypography  { FontSize = ".75rem",   FontWeight = "400", LineHeight = "2.66",  LetterSpacing = ".08333em" },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius  = "4px",
            DrawerWidthLeft      = "240px",
            DrawerWidthRight     = "240px",
            DrawerMiniWidthLeft  = "56px",
            DrawerMiniWidthRight = "56px",
            AppbarHeight         = "64px",
        },
    };
}
