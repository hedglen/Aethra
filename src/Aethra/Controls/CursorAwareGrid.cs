using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Aethra.Controls;

internal sealed class CursorAwareGrid : Grid
{
    private readonly InputCursor _arrowCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private readonly InputCursor _hiddenCursor = InputCursor.CreateFromCoreCursor(new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Custom, 0));

    internal void SetCursorVisible(bool isVisible)
    {
        ProtectedCursor = null;
        ProtectedCursor = isVisible ? _arrowCursor : _hiddenCursor;
    }
}
