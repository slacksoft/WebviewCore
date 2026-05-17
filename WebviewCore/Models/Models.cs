using AngleSharp.Dom;
using SkiaSharp;

namespace WebviewCore;

class LayoutBox
{
    public string Text { get; set; } = "";
    public RectangleF Bounds { get; set; }
    public bool IsBlock { get; set; }
    public bool IsLink { get; set; }
    public string? Href { get; set; }
    public float FontSize { get; set; } = 12;
    public Color Color { get; set; } = Color.Black;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool LineThrough { get; set; }
    public string FontName { get; set; } = "Segoe UI";

    public bool IsImage { get; set; }
    public string? ImageUrl { get; set; }
    public SKBitmap? ImageData { get; set; }
    public bool IsHr { get; set; }
    public bool IsInput { get; set; }
    public string? InputType { get; set; }
    public string? InputValue { get; set; }
    public string? InputName { get; set; }
    public bool InputChecked { get; set; }

    public BoxStyle? Style { get; set; }
    public IElement? Source { get; set; }
    public Color? BgColor { get; set; }
    public bool IsFixed { get; set; }
    public List<LayoutBox> Children { get; } = new();
}

class BoxStyle
{
    // ==================== Color & Background ====================
    public Color Color { get; set; } = Color.Black;
    public Color? BackgroundColor { get; set; }
    public string BackgroundImage { get; set; } = "";
    public string BackgroundRepeat { get; set; } = "repeat";
    public string BackgroundPosition { get; set; } = "0% 0%";
    public string BackgroundSize { get; set; } = "auto";
    public string BackgroundClip { get; set; } = "border-box";
    public string BackgroundOrigin { get; set; } = "padding-box";
    public string BackgroundAttachment { get; set; } = "scroll";
    public string BackgroundGradient { get; set; } = "";

    // ==================== Font ====================
    public float FontSize { get; set; } = 12;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool LineThrough { get; set; }
    public bool Overline { get; set; }
    public string FontFamily { get; set; } = "Segoe UI";
    public float LineHeight { get; set; }
    public float LetterSpacing { get; set; }
    public float WordSpacing { get; set; }
    public string TextTransform { get; set; } = "none";
    public float TextIndent { get; set; }
    public bool SmallCaps { get; set; }
    public string FontStretch { get; set; } = "normal";
    public string FontKerning { get; set; } = "auto";

    // ==================== Text Decoration ====================
    public string TextDecorationLine { get; set; } = "none";
    public Color TextDecorationColor { get; set; } = Color.Black;
    public string TextDecorationStyle { get; set; } = "solid";
    public float TextDecorationThickness { get; set; }

    // ==================== Text Shadow ====================
    public float TextShadowX { get; set; }
    public float TextShadowY { get; set; }
    public float TextShadowBlur { get; set; }
    public Color TextShadowColor { get; set; } = Color.Transparent;

    // ==================== Text Layout ====================
    public string WhiteSpace { get; set; } = "normal";
    public string TextAlign { get; set; } = "left";
    public string VerticalAlign { get; set; } = "baseline";
    public string Direction { get; set; } = "ltr";
    public string WordBreak { get; set; } = "normal";
    public string OverflowWrap { get; set; } = "normal";
    public string TextOverflow { get; set; } = "clip";
    public string WordWrap { get; set; } = "normal";
    public float TabSize { get; set; } = 8;
    public string Hyphens { get; set; } = "manual";

    // ==================== Display & Visibility ====================
    public bool DisplayBlock { get; set; } = true;
    public bool DisplayNone { get; set; }
    public bool DisplayInlineBlock { get; set; }
    public bool DisplayFlex { get; set; }
    public bool DisplayGrid { get; set; }
    public bool DisplayInlineFlex { get; set; }
    public float Opacity { get; set; } = 1f;
    public string Visibility { get; set; } = "visible";
    public string Overflow { get; set; } = "visible";
    public string OverflowX { get; set; } = "visible";
    public string OverflowY { get; set; } = "visible";
    public string PointerEvents { get; set; } = "auto";
    public string UserSelect { get; set; } = "auto";

    // ==================== Box Model ====================
    public float Width, Height;
    public bool HasWidth, HasHeight;
    public float MinWidth, MaxWidth, MinHeight, MaxHeight;
    public float PaddingTop, PaddingBottom, PaddingLeft, PaddingRight;
    public float MarginTop, MarginBottom, MarginLeft, MarginRight;
    public string BoxSizing { get; set; } = "content-box";

    // ==================== Border ====================
    public float BorderTop, BorderBottom, BorderLeft, BorderRight;
    public string BorderTopStyle { get; set; } = "solid";
    public string BorderBottomStyle { get; set; } = "solid";
    public string BorderLeftStyle { get; set; } = "solid";
    public string BorderRightStyle { get; set; } = "solid";
    public Color BorderColor { get; set; } = Color.Black;
    public Color BorderTopColor { get; set; } = Color.Black;
    public Color BorderBottomColor { get; set; } = Color.Black;
    public Color BorderLeftColor { get; set; } = Color.Black;
    public Color BorderRightColor { get; set; } = Color.Black;
    public float BorderRadius { get; set; }
    public float BorderTopLeftRadius { get; set; }
    public float BorderTopRightRadius { get; set; }
    public float BorderBottomLeftRadius { get; set; }
    public float BorderBottomRightRadius { get; set; }
    public bool BorderRadiusIsPercent { get; set; }
    public bool BorderTopLeftRadiusIsPercent { get; set; }
    public bool BorderTopRightRadiusIsPercent { get; set; }
    public bool BorderBottomLeftRadiusIsPercent { get; set; }
    public bool BorderBottomRightRadiusIsPercent { get; set; }

    // ==================== Outline ====================
    public float OutlineWidth { get; set; }
    public string OutlineStyle { get; set; } = "none";
    public Color OutlineColor { get; set; } = Color.Black;
    public float OutlineOffset { get; set; }

    // ==================== Box Shadow ====================
    public float ShadowX { get; set; }
    public float ShadowY { get; set; }
    public float ShadowBlur { get; set; }
    public float ShadowSpread { get; set; }
    public Color ShadowColor { get; set; } = Color.Transparent;
    public bool ShadowInset { get; set; }

    // ==================== Positioning ====================
    public string Position { get; set; } = "static";
    public float PositionLeft, PositionTop, PositionRight, PositionBottom;
    public int ZIndex { get; set; }

    // ==================== Float / Clear ====================
    public string Float { get; set; } = "none";
    public string Clear { get; set; } = "none";

    // ==================== Flexbox ====================
    public bool IsFlexContainer { get; set; }
    public string FlexDirection { get; set; } = "row";
    public string FlexWrap { get; set; } = "nowrap";
    public string JustifyContent { get; set; } = "flex-start";
    public string AlignItems { get; set; } = "stretch";
    public string AlignContent { get; set; } = "stretch";
    public float FlexGrow, FlexShrink = 1;
    public string FlexBasis { get; set; } = "auto";
    public string AlignSelf { get; set; } = "auto";
    public int Order;

    // ==================== Grid ====================
    public string GridTemplateColumns { get; set; } = "none";
    public string GridTemplateRows { get; set; } = "none";
    public string GridColumn { get; set; } = "auto";
    public string GridRow { get; set; } = "auto";
    public string GridGap { get; set; } = "normal";

    // ==================== Table ====================
    public bool IsTable, IsTableRow, IsTableCell;
    public int ColSpan = 1, RowSpan = 1;
    public string BorderCollapse { get; set; } = "collapse";
    public float BorderSpacing { get; set; }
    public string CaptionSide { get; set; } = "top";
    public string EmptyCells { get; set; } = "show";
    public string TableLayout { get; set; } = "auto";

    // ==================== Lists ====================
    public string ListStyleType { get; set; } = "disc";
    public string ListStylePosition { get; set; } = "outside";

    // ==================== Transform ====================
    public float TransformX, TransformY, TransformRotate;
    public float TransformScaleX = 1, TransformScaleY = 1;
    public float TransformSkewX, TransformSkewY;
    public float TransformOriginX = 0.5f, TransformOriginY = 0.5f;

    // ==================== Filter & Effects ====================
    public string Filter { get; set; } = "none";
    public float FilterBlur { get; set; }
    public float FilterBrightness { get; set; } = 1;
    public float FilterContrast { get; set; } = 1;
    public float FilterGrayscale { get; set; }
    public float FilterSepia { get; set; }
    public float FilterHueRotate { get; set; }
    public float FilterSaturate { get; set; } = 1;
    public float FilterInvert { get; set; }
    public float FilterOpacity { get; set; } = 1;
    public string MixBlendMode { get; set; } = "normal";
    public string BackdropFilter { get; set; } = "none";

    // ==================== Image & Object ====================
    public string ObjectFit { get; set; } = "fill";
    public string ObjectPosition { get; set; } = "50% 50%";
    public string ImageRendering { get; set; } = "auto";

    // ==================== Cursor ====================
    public string Cursor { get; set; } = "auto";

    // ==================== Transitions & Animations (stub) ====================
    public string Transition { get; set; } = "";
    public string TransitionProperty { get; set; } = "all";
    public float TransitionDuration { get; set; }
    public string Animation { get; set; } = "none";
}
