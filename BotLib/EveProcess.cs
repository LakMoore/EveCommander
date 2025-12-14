using read_memory_64_bit;
using System.Diagnostics;
using System.Runtime.Versioning;
using WindowsInput;

namespace BotLib
{
  public class EveProcess
  {

    int readingFromGameCount = 0;
    private static Stopwatch generalStopwatch = System.Diagnostics.Stopwatch.StartNew();

    private Queue<ReadingFromGameClient> readingFromGameHistory = new Queue<ReadingFromGameClient>();

    string ToStringBase16(byte[] array) => BitConverter.ToString(array).Replace("-", "");

    private Dictionary<int, SearchUIRootAddressTask> searchUIRootAddressTasks = new Dictionary<int, SearchUIRootAddressTask>();

    class SearchUIRootAddressTask
    {
      public Request.SearchUIRootAddressStructure request;

      public TimeSpan beginTime;

      public Response.SearchUIRootAddressCompletedStruct completed;

      public SearchUIRootAddressTask(Request.SearchUIRootAddressStructure request)
      {
        this.request = request;
        beginTime = generalStopwatch.Elapsed;

        System.Threading.Tasks.Task.Run(() =>
        {
          var uiTreeRootAddress = FindUIRootAddressFromProcessId(request.processId);

          completed = new Response.SearchUIRootAddressCompletedStruct
          {
            uiRootAddress = uiTreeRootAddress?.ToString()
          };
        });
      }
    }

    struct ReadingFromGameClient
    {
      public IntPtr windowHandle;

      public string readingId;
    }

    class Request
    {
      public object ListGameClientProcessesRequest;

      public SearchUIRootAddressStructure SearchUIRootAddress;

      public ReadFromWindowStructure ReadFromWindow;

      public class SearchUIRootAddressStructure
      {
        public int processId;
      }

      public class ReadFromWindowStructure
      {
        public string windowId;

        public ulong uiRootAddress;
      }


    }

    public class Response
    {
      public GameClientProcessSummaryStruct[] ListGameClientProcessesResponse;

      public SearchUIRootAddressResponseStruct SearchUIRootAddressResponse;

      public string FailedToBringWindowToFront;

      public object CompletedEffectSequenceOnWindow;

      public object CompletedOtherEffect;

      public record GameClientProcessSummaryStruct
      {
        public int processId;

        public long mainWindowId;

        public required string mainWindowTitle;

        public int mainWindowZIndex;
      }

      public class SearchUIRootAddressResponseStruct
      {
        public int processId;

        public SearchUIRootAddressStage stage;
      }


      public class SearchUIRootAddressStage
      {
        public SearchUIRootAddressInProgressStruct SearchUIRootAddressInProgress;

        public SearchUIRootAddressCompletedStruct SearchUIRootAddressCompleted;
      }

      public class SearchUIRootAddressInProgressStruct
      {
        public long searchBeginTimeMilliseconds;

        public long currentTimeMilliseconds;
      }


      public class SearchUIRootAddressCompletedStruct
      {
        public string uiRootAddress;
      }

    }

    static Response.SearchUIRootAddressStage SearchUIRootAddressTaskAsResponseStage(SearchUIRootAddressTask task)
    {
      return task.completed switch
      {
        Response.SearchUIRootAddressCompletedStruct completed =>
        new Response.SearchUIRootAddressStage { SearchUIRootAddressCompleted = completed },

        _ => new Response.SearchUIRootAddressStage
        {
          SearchUIRootAddressInProgress = new Response.SearchUIRootAddressInProgressStruct
          {
            searchBeginTimeMilliseconds = (long)task.beginTime.TotalMilliseconds,
            currentTimeMilliseconds = generalStopwatch.ElapsedMilliseconds,
          }
        }
      };
    }

    public static ulong? FindUIRootAddressFromProcessId(int processId)
    {
      var candidatesAddresses =
          EveOnline64.EnumeratePossibleAddressesForUIRootObjectsFromProcessId(processId);

      using (var memoryReader = new MemoryReaderFromLiveProcess(processId))
      {
        var uiTrees =
            candidatesAddresses
            .Select(candidateAddress => EveOnline64.ReadUITreeFromAddress(candidateAddress, memoryReader, 99))
            .ToList();

        return
            uiTrees
            .OrderByDescending(uiTree => uiTree?.EnumerateSelfAndDescendants().Count() ?? -1)
            .FirstOrDefault()
            ?.pythonObjectAddress;
      }
    }

    static void SetProcessDPIAware()
    {
      //  https://www.google.com/search?q=GetWindowRect+dpi
      //  https://github.com/dotnet/wpf/issues/859
      //  https://github.com/dotnet/winforms/issues/135
      WinApi.SetProcessDPIAware();
    }

    static public class SetForegroundWindowInWindows
    {
      static public int AltKeyPlusSetForegroundWindowWaitTimeMilliseconds = 60;

      /// <summary>
      /// </summary>
      /// <param name="windowHandle"></param>
      /// <returns>null in case of success</returns>
      [SupportedOSPlatform("windows5.0")]
      static public string? TrySetForegroundWindow(IntPtr windowHandle)
      {
        try
        {
          /*
          * For the conditions for `SetForegroundWindow` to work, see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow
          * */
          WinApi.SetForegroundWindow(windowHandle);

          if (WinApi.GetForegroundWindow() == windowHandle)
            return null;

          var windowsInZOrder = WinApi.ListWindowHandlesInZOrder();

          var windowIndex = windowsInZOrder.ToList().IndexOf(windowHandle);

          if (windowIndex < 0)
            return "Did not find window for this handle";

          var simulator = new InputSimulator();

          simulator.Keyboard.KeyDown(VirtualKeyCode.MENU);
          WinApi.SetForegroundWindow(windowHandle);
          simulator.Keyboard.KeyUp(VirtualKeyCode.MENU);

          System.Threading.Thread.Sleep(AltKeyPlusSetForegroundWindowWaitTimeMilliseconds);

          if (WinApi.GetForegroundWindow() == windowHandle)
            return null;

          return "Alt key plus SetForegroundWindow approach was not successful.";
        }
        catch (Exception e)
        {
          return "Exception: " + e.ToString();
        }
      }
    }

    struct Rectangle
    {
      public Rectangle(Int64 left, Int64 top, Int64 right, Int64 bottom)
      {
        this.left = left;
        this.top = top;
        this.right = right;
        this.bottom = bottom;
      }

      readonly public Int64 top, left, bottom, right;

    }

    static Process[] GetWindowsProcessesLookingLikeEVEOnlineClient() =>
        Process.GetProcessesByName("exefile");


    public static IReadOnlyList<GameClient> ListGameClientProcesses()
    {
      var allWindowHandlesInZOrder = WinApi.ListWindowHandlesInZOrder();

      int? zIndexFromWindowHandle(IntPtr windowHandleToSearch) =>
          allWindowHandlesInZOrder
          .Select((windowHandle, index) => (windowHandle, index: (int?)index))
          .FirstOrDefault(handleAndIndex => handleAndIndex.windowHandle == windowHandleToSearch)
          .index;

      var processes =
          GetWindowsProcessesLookingLikeEVEOnlineClient()
          .Select(process =>
          {
            return new GameClient
            {
              processId = process.Id,
              mainWindowId = process.MainWindowHandle,
              mainWindowTitle = process.MainWindowTitle,
              mainWindowZIndex = zIndexFromWindowHandle(process.MainWindowHandle) ?? 9999,
            };
          })
          .ToList();

      return processes;
    }
  }
}
