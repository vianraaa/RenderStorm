using System.Text;
using ImGuiNET;
using SDL2;

namespace RenderStorm.ImGuiImpl;

public static class ImGuiSdlInput
{
    public static Action<float>? OnScroll;
    public static bool CanScroll = true;
    public static ImGuiKey KeyEventToImGuiKey(SDL.SDL_Keycode keycode, SDL.SDL_Scancode scancode)
    {
        switch (keycode)
        {
            case SDL.SDL_Keycode.SDLK_TAB: return ImGuiKey.Tab;
            case SDL.SDL_Keycode.SDLK_LEFT: return ImGuiKey.LeftArrow;
            case SDL.SDL_Keycode.SDLK_RIGHT: return ImGuiKey.RightArrow;
            case SDL.SDL_Keycode.SDLK_UP: return ImGuiKey.UpArrow;
            case SDL.SDL_Keycode.SDLK_DOWN: return ImGuiKey.DownArrow;
            case SDL.SDL_Keycode.SDLK_PAGEUP: return ImGuiKey.PageUp;
            case SDL.SDL_Keycode.SDLK_PAGEDOWN: return ImGuiKey.PageDown;
            case SDL.SDL_Keycode.SDLK_HOME: return ImGuiKey.Home;
            case SDL.SDL_Keycode.SDLK_END: return ImGuiKey.End;
            case SDL.SDL_Keycode.SDLK_INSERT: return ImGuiKey.Insert;
            case SDL.SDL_Keycode.SDLK_DELETE: return ImGuiKey.Delete;
            case SDL.SDL_Keycode.SDLK_BACKSPACE: return ImGuiKey.Backspace;
            case SDL.SDL_Keycode.SDLK_SPACE: return ImGuiKey.Space;
            case SDL.SDL_Keycode.SDLK_RETURN: return ImGuiKey.Enter;
            case SDL.SDL_Keycode.SDLK_ESCAPE: return ImGuiKey.Escape;
            case SDL.SDL_Keycode.SDLK_QUOTE: return ImGuiKey.Apostrophe;
            case SDL.SDL_Keycode.SDLK_COMMA: return ImGuiKey.Comma;
            case SDL.SDL_Keycode.SDLK_MINUS: return ImGuiKey.Minus;
            case SDL.SDL_Keycode.SDLK_PERIOD: return ImGuiKey.Period;
            case SDL.SDL_Keycode.SDLK_SLASH: return ImGuiKey.Slash;
            case SDL.SDL_Keycode.SDLK_SEMICOLON: return ImGuiKey.Semicolon;
            case SDL.SDL_Keycode.SDLK_EQUALS: return ImGuiKey.Equal;
            case SDL.SDL_Keycode.SDLK_LEFTBRACKET: return ImGuiKey.LeftBracket;
            case SDL.SDL_Keycode.SDLK_BACKSLASH: return ImGuiKey.Backslash;
            case SDL.SDL_Keycode.SDLK_RIGHTBRACKET: return ImGuiKey.RightBracket;
            case SDL.SDL_Keycode.SDLK_BACKQUOTE: return ImGuiKey.GraveAccent;
            case SDL.SDL_Keycode.SDLK_CAPSLOCK: return ImGuiKey.CapsLock;
            case SDL.SDL_Keycode.SDLK_SCROLLLOCK: return ImGuiKey.ScrollLock;
            case SDL.SDL_Keycode.SDLK_NUMLOCKCLEAR: return ImGuiKey.NumLock;
            case SDL.SDL_Keycode.SDLK_PRINTSCREEN: return ImGuiKey.PrintScreen;
            case SDL.SDL_Keycode.SDLK_PAUSE: return ImGuiKey.Pause;
            case SDL.SDL_Keycode.SDLK_KP_0: return ImGuiKey.Keypad0;
            case SDL.SDL_Keycode.SDLK_KP_1: return ImGuiKey.Keypad1;
            case SDL.SDL_Keycode.SDLK_KP_2: return ImGuiKey.Keypad2;
            case SDL.SDL_Keycode.SDLK_KP_3: return ImGuiKey.Keypad3;
            case SDL.SDL_Keycode.SDLK_KP_4: return ImGuiKey.Keypad4;
            case SDL.SDL_Keycode.SDLK_KP_5: return ImGuiKey.Keypad5;
            case SDL.SDL_Keycode.SDLK_KP_6: return ImGuiKey.Keypad6;
            case SDL.SDL_Keycode.SDLK_KP_7: return ImGuiKey.Keypad7;
            case SDL.SDL_Keycode.SDLK_KP_8: return ImGuiKey.Keypad8;
            case SDL.SDL_Keycode.SDLK_KP_9: return ImGuiKey.Keypad9;
            case SDL.SDL_Keycode.SDLK_KP_PERIOD: return ImGuiKey.KeypadDecimal;
            case SDL.SDL_Keycode.SDLK_KP_DIVIDE: return ImGuiKey.KeypadDivide;
            case SDL.SDL_Keycode.SDLK_KP_MULTIPLY: return ImGuiKey.KeypadMultiply;
            case SDL.SDL_Keycode.SDLK_KP_MINUS: return ImGuiKey.KeypadSubtract;
            case SDL.SDL_Keycode.SDLK_KP_PLUS: return ImGuiKey.KeypadAdd;
            case SDL.SDL_Keycode.SDLK_KP_ENTER: return ImGuiKey.KeypadEnter;
            case SDL.SDL_Keycode.SDLK_KP_EQUALS: return ImGuiKey.KeypadEqual;
            case SDL.SDL_Keycode.SDLK_LCTRL: return ImGuiKey.LeftCtrl;
            case SDL.SDL_Keycode.SDLK_LSHIFT: return ImGuiKey.LeftShift;
            case SDL.SDL_Keycode.SDLK_LALT: return ImGuiKey.LeftAlt;
            case SDL.SDL_Keycode.SDLK_LGUI: return ImGuiKey.LeftSuper;
            case SDL.SDL_Keycode.SDLK_RCTRL: return ImGuiKey.RightCtrl;
            case SDL.SDL_Keycode.SDLK_RSHIFT: return ImGuiKey.RightShift;
            case SDL.SDL_Keycode.SDLK_RALT: return ImGuiKey.RightAlt;
            case SDL.SDL_Keycode.SDLK_RGUI: return ImGuiKey.RightSuper;
            case SDL.SDL_Keycode.SDLK_APPLICATION: return ImGuiKey.Menu;
            case SDL.SDL_Keycode.SDLK_0: return ImGuiKey._0;
            case SDL.SDL_Keycode.SDLK_1: return ImGuiKey._1;
            case SDL.SDL_Keycode.SDLK_2: return ImGuiKey._2;
            case SDL.SDL_Keycode.SDLK_3: return ImGuiKey._3;
            case SDL.SDL_Keycode.SDLK_4: return ImGuiKey._4;
            case SDL.SDL_Keycode.SDLK_5: return ImGuiKey._5;
            case SDL.SDL_Keycode.SDLK_6: return ImGuiKey._6;
            case SDL.SDL_Keycode.SDLK_7: return ImGuiKey._7;
            case SDL.SDL_Keycode.SDLK_8: return ImGuiKey._8;
            case SDL.SDL_Keycode.SDLK_9: return ImGuiKey._9;
            case SDL.SDL_Keycode.SDLK_a: return ImGuiKey.A;
            case SDL.SDL_Keycode.SDLK_b: return ImGuiKey.B;
            case SDL.SDL_Keycode.SDLK_c: return ImGuiKey.C;
            case SDL.SDL_Keycode.SDLK_d: return ImGuiKey.D;
            case SDL.SDL_Keycode.SDLK_e: return ImGuiKey.E;
            case SDL.SDL_Keycode.SDLK_f: return ImGuiKey.F;
            case SDL.SDL_Keycode.SDLK_g: return ImGuiKey.G;
            case SDL.SDL_Keycode.SDLK_h: return ImGuiKey.H;
            case SDL.SDL_Keycode.SDLK_i: return ImGuiKey.I;
            case SDL.SDL_Keycode.SDLK_j: return ImGuiKey.J;
            case SDL.SDL_Keycode.SDLK_k: return ImGuiKey.K;
            case SDL.SDL_Keycode.SDLK_l: return ImGuiKey.L;
            case SDL.SDL_Keycode.SDLK_m: return ImGuiKey.M;
            case SDL.SDL_Keycode.SDLK_n: return ImGuiKey.N;
            case SDL.SDL_Keycode.SDLK_o: return ImGuiKey.O;
            case SDL.SDL_Keycode.SDLK_p: return ImGuiKey.P;
            case SDL.SDL_Keycode.SDLK_q: return ImGuiKey.Q;
            case SDL.SDL_Keycode.SDLK_r: return ImGuiKey.R;
            case SDL.SDL_Keycode.SDLK_s: return ImGuiKey.S;
            case SDL.SDL_Keycode.SDLK_t: return ImGuiKey.T;
            case SDL.SDL_Keycode.SDLK_u: return ImGuiKey.U;
            case SDL.SDL_Keycode.SDLK_v: return ImGuiKey.V;
            case SDL.SDL_Keycode.SDLK_w: return ImGuiKey.W;
            case SDL.SDL_Keycode.SDLK_x: return ImGuiKey.X;
            case SDL.SDL_Keycode.SDLK_y: return ImGuiKey.Y;
            case SDL.SDL_Keycode.SDLK_z: return ImGuiKey.Z;
            case SDL.SDL_Keycode.SDLK_F1: return ImGuiKey.F1;
            case SDL.SDL_Keycode.SDLK_F2: return ImGuiKey.F2;
            case SDL.SDL_Keycode.SDLK_F3: return ImGuiKey.F3;
            case SDL.SDL_Keycode.SDLK_F4: return ImGuiKey.F4;
            case SDL.SDL_Keycode.SDLK_F5: return ImGuiKey.F5;
            case SDL.SDL_Keycode.SDLK_F6: return ImGuiKey.F6;
            case SDL.SDL_Keycode.SDLK_F7: return ImGuiKey.F7;
            case SDL.SDL_Keycode.SDLK_F8: return ImGuiKey.F8;
            case SDL.SDL_Keycode.SDLK_F9: return ImGuiKey.F9;
            case SDL.SDL_Keycode.SDLK_F10: return ImGuiKey.F10;
            case SDL.SDL_Keycode.SDLK_F11: return ImGuiKey.F11;
            case SDL.SDL_Keycode.SDLK_F12: return ImGuiKey.F12;
            case SDL.SDL_Keycode.SDLK_F13: return ImGuiKey.F13;
            case SDL.SDL_Keycode.SDLK_F14: return ImGuiKey.F14;
            case SDL.SDL_Keycode.SDLK_F15: return ImGuiKey.F15;
            case SDL.SDL_Keycode.SDLK_F16: return ImGuiKey.F16;
            case SDL.SDL_Keycode.SDLK_F17: return ImGuiKey.F17;
            case SDL.SDL_Keycode.SDLK_F18: return ImGuiKey.F18;
            case SDL.SDL_Keycode.SDLK_F19: return ImGuiKey.F19;
            case SDL.SDL_Keycode.SDLK_F20: return ImGuiKey.F20;
            case SDL.SDL_Keycode.SDLK_F21: return ImGuiKey.F21;
            case SDL.SDL_Keycode.SDLK_F22: return ImGuiKey.F22;
            case SDL.SDL_Keycode.SDLK_F23: return ImGuiKey.F23;
            case SDL.SDL_Keycode.SDLK_F24: return ImGuiKey.F24;
            case SDL.SDL_Keycode.SDLK_AC_BACK: return ImGuiKey.AppBack;
            case SDL.SDL_Keycode.SDLK_AC_FORWARD: return ImGuiKey.AppForward;
            default: break;
        }
        return ImGuiKey.None;
    }
    public static bool Process(SDL.SDL_Event e)
    {
        var io = ImGui.GetIO();
        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_MOUSEMOTION:
            {
                io.AddMouseSourceEvent(ImGuiMouseSource.Mouse);
                io.AddMousePosEvent(e.motion.x, e.motion.y);
                return true;
            }
            case SDL.SDL_EventType.SDL_MOUSEWHEEL:
            {
                float wheel_x = -(float)e.wheel.x;
                float wheel_y = (float)e.wheel.y;
                OnScroll?.Invoke(wheel_y);
                if (!CanScroll) return true;
                io.AddMouseSourceEvent(e.wheel.which == SDL.SDL_TOUCH_MOUSEID
                    ? ImGuiMouseSource.TouchScreen
                    : ImGuiMouseSource.Mouse);
                io.AddMouseWheelEvent(wheel_x, wheel_y);
                return true;
            }
            case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
            case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
            {
                int mouse_button = -1;
                if (e.button.button == SDL.SDL_BUTTON_LEFT) { mouse_button = 0; }
                if (e.button.button == SDL.SDL_BUTTON_RIGHT) { mouse_button = 1; }
                if (e.button.button == SDL.SDL_BUTTON_MIDDLE) { mouse_button = 2; }
                if (e.button.button == SDL.SDL_BUTTON_X1) { mouse_button = 3; }
                if (e.button.button == SDL.SDL_BUTTON_X2) { mouse_button = 4; }
                if (mouse_button == -1)
                    break;
                io.AddMouseSourceEvent(e.button.which == SDL.SDL_TOUCH_MOUSEID ? ImGuiMouseSource.TouchScreen : ImGuiMouseSource.Mouse);
                io.AddMouseButtonEvent(mouse_button, (e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN));
                return true;
            }
            case SDL.SDL_EventType.SDL_TEXTINPUT:
            {
                unsafe
                {
                    int i = 0;
                    byte c = e.text.text[i];
                    io.AddInputCharacter(c);
                }
                return true;
            }
            case SDL.SDL_EventType.SDL_KEYDOWN:
            case SDL.SDL_EventType.SDL_KEYUP:
            {
                /*ImGuiSdl2Impl.ImGui_ImplSDL2_UpdateKeyModifiers(e.key.keysym.mod);*/
                ImGuiKey key = KeyEventToImGuiKey(e.key.keysym.sym, e.key.keysym.scancode);
                io.AddKeyEvent(key, (e.type == SDL.SDL_EventType.SDL_KEYDOWN));
                io.SetKeyEventNativeData(key, (int)e.key.keysym.sym, (int)e.key.keysym.scancode, (int)e.key.keysym.scancode); // To support legacy indexing (<1.87 user code). Legacy backend uses SDLK_*** as indices to IsKeyXXX() functions.
                return true;
            }

        }

        return false;
    }
}