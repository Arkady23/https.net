//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!                                                     !!
//!!   https.net сервер на C#.    Автор: A.Б.Корниенко   !!
//!!   Головной блок              версия от 26.07.2026   !!
//!!                                                     !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

using https1;
using https2;
using System;
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Buffers;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Buffers.Text;
using System.Net.Security;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

public class F : Form {
    public static readonly Encoding UTF8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    ToolStripMenuItem menuQ = new ToolStripMenuItem();
    ToolStripMenuItem menuF = new ToolStripMenuItem();
    ToolStripMenuItem menuS = new ToolStripMenuItem();
    ToolStripMenuItem menuR = new ToolStripMenuItem();
    ContextMenuStrip menu = new ContextMenuStrip();
    static readonly object logFlush = new object();
    public static ConcurrentStack<int> freeCGI;
    public static ConcurrentStack<int> freeVFP;
    IContainer conta = new Container();
    static Server ser;
    NotifyIcon nIcon;
    TextBox textBox1;
    string[] param;

    const string hn="https.net";
    const string hs=hn+" server", fn=hn+".xml", leftSp="                       \t";
    public const string DI="index.html", stopIconText= hs+" is stopped", initCGI= "initcgi.",
                 logX=hn+".x.log", logY=hn+".y.log", DirectorySessions="Sessions",
           //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                 ver="version 2.2.1", verD="July 2026";       //!!
           //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    public const  int i8=1500000, i9=2147483647;
    public static int i, k, port, port1, post, st, qu, bu, bu2, db, db1, it, it1, log9, logi=0,
                  st1, stf, qu1, tw, iIP, iIP1, maxVFP,
                  HeaderBufSize= 1024,    // 1 KB  - первоначальный размер буфера заголовков
                  HeaderBufSize2= 3072,   // 3 KB  - последующий размер буфера заголовков
                  MaxHeaderSize= 29696;   // 29 KB - максимальный размер под заголовки
                                          //         за минусом текущего буфера
    public static string DocumentRoot, Folder=Thread.GetDomain().BaseDirectory,
                  DirectoryIndex, Proc, Args, Ext, logZ=string.Empty;
    static readonly Channel<ReadOnlyMemory<char>> logQueue =
                  Channel.CreateUnbounded<ReadOnlyMemory<char>>(
                  new UnboundedChannelOptions { SingleReader = true });
    private static string Fullexe = Folder+hn+".exe";
    public static bool notExit=false, notQuit=true, cgia, VFP9, VFPclr;
    public static Icon ico = Icon.ExtractAssociatedIcon(Fullexe);
    public static readonly byte[] CachedDateBytes = new byte[29];
    public static SslServerAuthenticationOptions cert = null;
    public static IPAddress IP = IPAddress.None;
    public static StreamWriter logSW = null;
    public static Session[] session = null;
    public static FileStream logFS = null;
    public static ProcessStartInfo[] cgi;
    public static dynamic[] vfp = null;
    public static byte[] vfpb, cgib;
    public static Type vfpa = null;
    public static Process[] proc;
    public static int[] vfpi;
    int a9=1000, s9=32767;
    string CerFile;

    protected override void Dispose( bool disposing ) {
      // Clean up any container being used.
      if( disposing )
          if (conta != null) conta.Dispose();            
      base.Dispose( disposing );
    }

    void nIcon_BalloonTipClosed(object Sender, EventArgs e) {

      // Отображались ошибки в параметрах запуска
      this.Close();
    }

    void nIcon_BalloonTipClicked(object Sender, EventArgs e) {

      // Отображались ошибки в параметрах запуска
      this.Close();
    }

    void nIcon_DoubleClick(object Sender, EventArgs e) {
      // Set the WindowState to normal if the form is minimized.
      if(this.WindowState == FormWindowState.Minimized) {
         this.Show();
         this.WindowState = FormWindowState.Normal;
      }
      this.CenterToScreen();
      this.Activate();
    }
    void menuS_Click(object Sender, EventArgs e) {
      if(nIcon.Text==stopIconText) RunServer(param);
    }
    void menuF_Click(object Sender, EventArgs e) {
      StopServer();
    }
    void menuR_Click(object Sender, EventArgs e) {
      StopServer();
      RunServer(param);
    }
    void menuQ_Click(object Sender, EventArgs e) {
      notQuit = false;
      StopServer();
    }
    void StopIcon() {
      // Отобразить значок выключения
      if(notQuit) {
        nIcon.Icon = SystemIcons.Exclamation;
        nIcon.Text = stopIconText;
      }
    }

    [STAThread]
    static void Main (string[] args) {

      // Включаем поддержку непредусмотренных кодировок для всего приложения
      // Обязательно указывается в первой строке:
      System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

      Directory.SetCurrentDirectory(Folder);
      if(!(ico != null)) ico = SystemIcons.Shield;

      // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-9.0&redirectedfrom=MSDN
      Application.Run( new F(args));

    }

    public F(string[] args) {
      this.WindowState = FormWindowState.Minimized;
      this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
      InitLogging();   // Включаем журнал
      this.FormClosing += Form_Close;
      this.ShowInTaskbar = false;
      this.Shown += Form_Shown;

      // Initialize menu
      this.menu.Items.AddRange( new ToolStripMenuItem[] {this.menuR,this.menuS,this.menuF,this.menuQ});

      // Убираем левую область для иконок
      this.menu.ShowCheckMargin = false;
      this.menu.ShowImageMargin = false;

      this.menuR.Text = "R&eload";
      this.menuR.Click += new EventHandler(this.menuR_Click);

      this.menuS.Text = "S&tart";
      this.menuS.Click += new EventHandler(this.menuS_Click);

      this.menuF.Text = "F&inalize";
      this.menuF.Click += new EventHandler(this.menuF_Click);

      this.menuQ.Text = "Q&uit";
      this.menuQ.Click += new EventHandler(this.menuQ_Click);

      // Set up how the form should be displayed.
      this.ClientSize = new Size(900,650);
      this.Text = hs;

      // Create the NotifyIcon.
      this.nIcon = new NotifyIcon(this.conta);

      // The Icon property sets the icon that will appear
      // in the systray for this application.
      nIcon.Icon = ico;

      // The ContextMenu property sets the menu that will
      // appear when the systray icon is right clicked.
      nIcon.ContextMenuStrip = this.menu;

      // The Text property sets the text that will be displayed,
      // in a tooltip, when the mouse hovers over the systray icon.
      nIcon.Text = hs+" is starting...";
      nIcon.Visible = true;

      // Событие закрытия уведомления
      nIcon.BalloonTipClosed += new EventHandler(nIcon_BalloonTipClosed);
      nIcon.BalloonTipClicked += new EventHandler(nIcon_BalloonTipClicked);

      // Handle the DoubleClick event to activate the form.
      nIcon.DoubleClick += new EventHandler(this.nIcon_DoubleClick);

      AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
      {
        notQuit = false;
        StopServer();
      };

      // Анонимная функция перехвата и вывода ошибки
      AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
      {
        log("\t"+((Exception)eventArgs.ExceptionObject).ToString());
        StopServer();
      };

      param = (string[])args.Clone();
      ThreadPool.GetMaxThreads(out s9, out a9);
      RunServer(args);
    }

    void Form_Shown(object sender, EventArgs e) {
      if(this.WindowState == FormWindowState.Minimized) {
        this.Hide();
      } else {
        this.Show();
      }
    }

    void Form_Close(object sender, CancelEventArgs e) {
      if (notQuit && session != null) {
        e.Cancel = true;  // кнопка больше не закрывает форму
        this.WindowState = FormWindowState.Minimized;
        this.Hide();
      }
    }

    private async void RunServer(string[] args){

      // Установить значения сервера по умолчанию
      CerFile="kornienko.ru.pfx";
      DocumentRoot="../www/";
      Proc="python.exe";
      DirectoryIndex=DI;
      Args=string.Empty;
      post=33554432;
      iIP=iIP1=0;
      log9=10000;
      port1=8080;
      port=8443;
      bu=131072;
      Ext="pyc";
      db=it=32;
      tw=5000;
      qu=1500;
      st=500;
      qu1=32;
      st1=16;
      db1=2;
      it1=2;

      if(getArgs(args)){
        if(notQuit) {
          InitLogging2();
          if(Args.Length > 0) Args+=" ";

          // Создать объект cert
          if(!File.Exists(CerFile)) {
            CerFile=DocumentRoot+CerFile;
            if(!File.Exists(CerFile)) CerFile=string.Empty;
          }
          if(CerFile==string.Empty) {
            log("\tCertificate was not found.");
            port = 0;
          } else {
            try {
              cert = new SslServerAuthenticationOptions {
                     ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(CerFile,
                     string.Empty),      // Пароль не используется
                     CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                     EnabledSslProtocols = SslProtocols.Tls13,
                     ClientCertificateRequired = false };
            } catch(Exception e) {
              log($"\tCertificate error: {e.Message}");
              cert = null;
            }
            if(!(cert!=null)) port=0;
          }
          if(port>0 || port1>0) {
            // Вычислить размер поля и формата в журнал для записи номеров сессий
            stf = st.ToString().Length + 1;

            // Буфер должен быть больше максимально возможного заголовка
            if(MaxHeaderSize + HeaderBufSize2 > bu) {
              bu2 = bu / 7;
              if      (bu2 < 64)  HeaderBufSize = 64;
              else if (bu2 < 128) HeaderBufSize = 128;
              else if (bu2 < 256) HeaderBufSize = 256;
              else if (bu2 < 512) HeaderBufSize = 512;
              else                HeaderBufSize = 1024;
              HeaderBufSize2 = HeaderBufSize*3;
              MaxHeaderSize = HeaderBufSize + HeaderBufSize2;
              bu = MaxHeaderSize + HeaderBufSize2;
            }

            // Разделить буфер для ускорения чтения
            bu2 = (bu-1)/2;

            // Создать объекты сессий предварительно очистив сессии от предыдущих запусков
            i = st;         // Начальное число соединений
            ThreadPool.SetMinThreads(i,a9);
            session = new Session[i];
            try{
              ParallelOptions options = new ParallelOptions() {
                 MaxDegreeOfParallelism = Environment.ProcessorCount * 2 
              };
              Parallel.For(0, i, options, j => { 
                 session[j] = new Session(j); 
              });
              notExit=true;
            } catch {
              log("\tThere were problems when creating threads. Try updating Windows.");
            }
          }
        }
        if(notExit) {
          // Запустить экземпляр CGI
          cgib = new byte[it];
          proc = new Process[it];
          cgi = new ProcessStartInfo[it];
          cgia = !start_CGI(0,1);
          if(cgia) {
            if(it1>0) {
              if(it1>db) it1=it;
              for (i=1; i<it1; i++) if(start_CGI(i,1)) break;
            } else {
              cgiQuit(in it1);
              cgib[0]=0;
            }

            // Свободные номера просессов для CGI
            freeCGI = new ConcurrentStack<int>();
            for (i=it; i>0; ) freeCGI.Push(--i);

          } else {
            log("\tThe \""+Proc+("\" interpreter or\r\n".PadRight(41))+
                "\tthe \""+DocumentRoot+initCGI+Ext+"\" script could not be run.");
          }

          // Запустить и настроить экземпляр FoxPro9
          VFPclr = false;
          vfpa = Type.GetTypeFromProgID("foxpro9.Shell");
          if(vfpa!=null){
            vfp = new dynamic[db];
            vfpb = new byte[db];
            vfpi = new int[db];
            try {
              vfp[0] = Activator.CreateInstance(vfpa);
              vfpb[0]= 1;
            } catch {
              log("\tCOM server \"foxpro9.Shell\" is not registered in Windows registry.");
              vfpa = null;
            }
            if(vfpa!=null){

              VFP9= vfp[0].Eval("sys(17)")=="Pentium";
              maxVFP= VFP9? 16777184 : 67108832;
              VFPclr= vfp[0].Eval("file(THIS.VFPclear)");
              vfpi[0]= vfp[0].ProcessID;

              // Свободные номера баз данных
              freeVFP= new ConcurrentStack<int>();
              for (i=db; i>0; ) freeVFP.Push(--i);
            }
          }

          // Создать начальное количество COM FoxPro9
          if(vfpa!=null){
            if(db1>0) {
              if(db1>db) db1=db;
              for (i=1; i<db1; i++) if(start_VFP(i,1)) break;
            } else {
              vfpQuit(in db1);
              vfpb[0]=0;
            }
          }

          // Запускаем движок https
          if(Directory.Exists(DirectorySessions)) Directory.Delete(DirectorySessions,true);
          IPEndPoint ep1 = new IPEndPoint(IPAddress.IPv6Any, port1);
          IPEndPoint ep = new IPEndPoint(IPAddress.IPv6Any, port);
          ser = new Server();
          if(ser.Start(ep,ep1)) {

            // Отобразить значок работы
            nIcon.Icon = ico;  // SystemIcons.Shield;
            nIcon.Text = $"{hs} is running";
            string pp = (port > 0 && port1 > 0) ? "Both https- and http" :
                        (port > 0 ? "https" : "http");
            log($"\tThe {hs} {ver} is running.\r\n{leftSp}{pp}-sessions are available.");

          } else {
            notExit = false;   // Отметить для возможности снятия, т.к. сервер запущен

            // Отобразить значок выключения
            this.StopIcon();
          }
        } else {

          // Отобразить значок выключения
          this.StopIcon();
        }

      }else{

        // Неверные параметры запуска, закрыть приложение
        nIcon.Text = hs;
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.CenterToScreen();
        this.Activate();
      }
    }

    public void StopServer(){
      if(notExit){
        notExit = false;

        // Остановить движок
        ser.Stop();

        // Отобразить значок выключения
        this.StopIcon();

        // Закрыть все процессы интерпретатора
        if(cgia) for(i=0; i<it; i++) if(cgib[i]>0) cgiQuit(in i);
        proc = null;
        cgib = null;
        cgi = null;

        // Закрыть все процессы VFP
        if(vfpa != null) for(i=0; i<db; i++) if(vfpb[i]>0) vfpQuit(in i);
        vfpb = null;
        vfpa = null;
        vfpi = null;
        vfp = null;
    
        log("\tThe "+stopIconText+".");
      }
      if(!notQuit) this.Close();
    }

    static void cgiQuit(in int i) {
       try{ proc[i].StandardInput.WriteLine(string.Empty); }
       catch { }
    }

    public static void vfpQuit(in int i) {
      if(vfp[i] != null) {
        try {
          if(Marshal.IsComObject(vfp[i]))
           Marshal.FinalReleaseComObject(vfp[i]);
        } finally {
          vfp[i] = null;
        }
      }
    }

    static void InitLogging() {
      lock (logFlush) {
        if(logFS == null) {
           logZ = (File.GetLastWriteTime(logX) <= File.GetLastWriteTime(logY)) ? logX : logY;
           log1();
        }
      }
    }

    public static void log(object x) {
      // Ваша отладочная логика синхронизации файлов остается неизменной
      lock (logFlush) {
        try {
          if(log9 > 0 && logi >= log9) {
            logA();
          } else if (log9 > 0) {
            logi++;
          }

          // Вызываем специальную отладочную перегрузку logB
          logBDebug(x);

          // Принудительный сброс на диск — гарантирует, что при падении лог сохранится
          logSW?.Flush();
          logFS?.Flush();
        } catch (ObjectDisposedException) {
          log9 = 0;
        }
      }
    }

    // Специальная перегрузка logB для отладки (принимает любой тип данных)
    private static void logBDebug(object x) {
      // Форматируем время на стеке, чтобы не мусорить в куче хотя бы на дате
      Span<char> timeBuffer = stackalloc char[23];
      if(DateTime.Now.TryFormat(timeBuffer, out _, "dd.MM.yyyy HH:mm:ss.fff")) {
        Console.Out.Write(timeBuffer);
      }

      // Извлекаем строку из объекта (для отладки это допустимо)
      string message = x?.ToString() ?? "null";
      Console.Out.Write(message);
      Console.Out.WriteLine();
    }

    // ВТОРАЯ ИНИЦИАЛИЗАЦИЯ (Вызывать, когда считали конфиг и уже известно значение log9)
    // Запускает фоновые задачи записи и таймер сброса для высоконагруженного F.log2()
    static void InitLogging2() {
      if(log9 > 0) {
        // Запускаем асинхронный цикл обработки очереди в фоне.
        // Опция LongRunning подсказывает .NET, что эта задача будет жить вечно, 
        // и планировщик выделит под неё оптимальный ресурс ThreadPool.
        Task.Factory.StartNew(
            WriteLoopAsync, 
            CancellationToken.None, 
            TaskCreationOptions.LongRunning, 
            TaskScheduler.Default
        );
      }

      // Запускаем асинхронный таймер сброса буферов на диск раз в 2 секунды.
      // Так как он большую часть времени спит, обычного Task.Run более чем достаточно.
      _ = Task.Run(FlushLoopAsync);
    }

    // Полностью асинхронный фоновый обработчик очереди (0 блокировок потоков)
    static async Task WriteLoopAsync() {
      Thread.CurrentThread.Priority = ThreadPriority.Lowest;

      var reader = logQueue.Reader;
      try {
        await foreach (ReadOnlyMemory<char> x in reader.ReadAllAsync().ConfigureAwait(false)) {
          try {
            if(logi >= log9 && logFS != null) {
              logA();
            } else {
              logi++;
            }
            if(logFS == null) {
              logZ = (File.GetLastWriteTime(logX) <= File.GetLastWriteTime(logY)) ? logX : logY;
              log1();
            }
            try {
              // 1. Физически записываем лог на диск через StreamWriter
              logB(x); 
            } finally {
              // 2. ВСТАВЛЯЕМ СЮДА: Возврат массива в пул после успешной записи
              if(MemoryMarshal.TryGetArray(x, out ArraySegment<char> segment)) {
                if(segment.Array != null) {
                  ArrayPool<char>.Shared.Return(segment.Array);
                }
              }
            }
          } catch (ObjectDisposedException) {
            log9 = 0;
          }
        }
      } catch (OperationCanceledException) {
        // Штатный выход, если канал или поток выполнения был отменен
      }
    }

    // Таймер сброса буфера на диск раз в 2 секунды
    static async Task FlushLoopAsync() {
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    
      // Чистый асинхронный цикл — 0 блокировок потоков ОС
      while (await timer.WaitForNextTickAsync().ConfigureAwait(false)) {
        if(log9 > 0) {
          try {
            // Оставляем lock, так как этот таймер работает параллельно 
            // с фоновым потоком записи WriteLoop
            lock (logFlush) {
              if(logSW != null && logFS != null) {
                // Сбрасываем буферы на диск
                logSW.Flush();
                logFS.Flush();
              }
            }
          } catch { }
        }

        // Актуальное время для заголовка Date (работает со стеком/буфером, 0 аллокаций)
        // Предполагается, что CachedDateBytes — это Span<char> или Span<byte> (для UTF-8 используется UTF8.TryFormat)
        DateTime.UtcNow.TryFormat(CachedDateBytes, out _, "R");
      }
    }

    internal static void log1() {
      // Настраиваем FileStream с оптимизированным размером буфера для частой записи
      logFS = new FileStream(logZ, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
    
      // Инициализируем StreamWriter строго в UTF-8 без BOM (стандарт для логов)
      logSW = new StreamWriter(logFS, UTF8);
    
      // Перенаправляем потоки вывода C#. Теперь любой Console.Write будет писать напрямую в файл лога
      Console.SetError(logSW);
      Console.SetOut(logSW);
    }

    internal static void logA() {
      logi = 1;
      logZ = (logY == logZ) ? logX : logY;
      logSW?.Close();
      logFS?.Close();
      log1();
    }

    internal static void logB(ReadOnlyMemory<char> x) {
      Span<char> timeBuffer = stackalloc char[23];
      if(DateTime.Now.TryFormat(timeBuffer, out int charsWritten, "dd.MM.yyyy HH:mm:ss.fff")) {
        Console.Out.Write(timeBuffer);
        Console.Out.Write(x.Span);
        Console.Out.WriteLine();
      } else {
        Console.Out.Write(x.Span);
        Console.Out.WriteLine(); 
      }
    }

    // МЕТОД 2: Высоконагруженный фоновый логгер.
    public static void log2(ReadOnlyMemory<char> x) {
      if (log9 > 0) logQueue.Writer.TryWrite(x);
    }

    // Проверки IP
    public static bool ifIP(IPAddress incomingIP) {
      if(incomingIP.Equals(Volatile.Read(ref IP))) return true;
      iIP = iIP1 = 0;                     // Если был другой IP, то сбрасываем счетчики
      return false;
    }

    // Уменьшить счетчик IP
    public static void DecrIP(IPAddress incomingIP) {
      if(ifIP(incomingIP)) Interlocked.Decrement(ref iIP);
    }

    // Уменьшить счетчик IP1
    public static void DecrIP1(IPAddress incomingIP) {
      if(ifIP(incomingIP)) Interlocked.Decrement(ref iIP1);
    }

    public static int valInt(string x, int maxLength = 11) {
      if(string.IsNullOrEmpty(x)) return i9;
      if(int.TryParse(x.AsSpan(0, x.Length < maxLength ? x.Length : maxLength),
                      out int z)) return z;
      return i9;
    }

    public static int valInt(ReadOnlySpan<byte> x) {
      if(x.IsEmpty) return i9;
      if(Utf8Parser.TryParse(x, out int z, out int i)) {
        if(i == x.Length || x[i] == 32) return z;
      }
      return i9;
    }

    // Запуск скрипта initCGI
    public static bool start_CGI(int i, byte b=2) {
      // Если процесс не работает, то запустим
      cgi[i] = new ProcessStartInfo();
      cgi[i].FileName = Proc;
      cgi[i].CreateNoWindow = true;
      cgi[i].UseShellExecute = false;
      cgi[i].RedirectStandardInput = true;
      cgi[i].RedirectStandardOutput = true;
      cgi[i].Arguments = Args+" \""+DocumentRoot+initCGI+Ext+"\"";
      try {
        proc[i] = Process.Start(cgi[i]);
        cgib[i] = b;
        return false;
      } catch { }
      return true;
    }

    // Подготовим CGI к новым заданиям
    public static void clear_cgi(int m) {
      if(proc[m] != null) {
        try { proc[m].Dispose(); } catch { }
        proc[m] = null;
      }
      cgib[m] = start_CGI(m)? (byte)0: (byte)1;
      freeCGI.Push(m);
    }

    // Запуск VFP
    public static bool start_VFP(int m, byte b=2) {
      try {
        vfp[m]= Activator.CreateInstance(vfpa);
        vfpi[m]= vfp[m].ProcessID;
        vfpb[m]= b;
        return false;
      } catch { }
      return true;
    }

    // Подготовим VFP к новым заданиям
    public static void clear_prg(int m) {
      if(vfpb[m]>0) {
        try {
          if(vfp[m].clearPRG(VFPclr)) {
            vfpQuit(in m);
            _= start_VFP(m,1);
          }
        } catch {
          vfpQuit(in m);
          vfpb[m]=0;
        }
      }
      if(vfpb[m]==0) {
        killVFP(m);
        _= start_VFP(m,1);
      }
      freeVFP.Push(m);
    }

    // Аварийно снимаем COM-процесс
    static void killVFP(int m) {
      try { Process.GetProcessById(vfpi[m]).Kill(); }
      catch { }
      vfpQuit(in m);
    }

    // Выполнить команду "schtasks"
    bool schtasks(ref string par){
      bool ret;
      string output;
      byte[] buf = new byte[100];
      var ps = new ProcessStartInfo();
      ps.FileName = "schtasks";
      ps.CreateNoWindow = true;
      ps.UseShellExecute = false;
      ps.RedirectStandardOutput = true;
      ps.Arguments = par;
      try {
        Process p = Process.Start(ps);
        output = Encoding.GetEncoding(866).GetString(buf, 0,
                 p.StandardOutput.BaseStream.Read(buf,0,100));
        p.WaitForExit();
        ret = true;
      } catch {
        output = "FAILED :-(";
        ret = false;
      }
      if(output.Length>2) {
        nIcon.ShowBalloonTip(6100, "Schtasks command", output,
              ret? ToolTipIcon.Info:ToolTipIcon.Error);
      }
      return ret;
    }

    int odd(string z) {
      return (z.Length - z.Replace("'", string.Empty).Length)%2 +
             (z.Length - z.Replace("\"", string.Empty).Length)%2;
    }

    string toStd(string z) {
      return z.Contains(" ")? "\""+z+"\"": z;
    }

    bool toArg(string[] args) {
      return ++i<args.Length;
    }

    bool getArgs(String[] args){
      const int b9=262144, p9=65535, post9=33554432, b0=512, log0=80;
      string tx=string.Empty, ts=string.Empty, cA="Arguments>";
      int k1, t9=10;
      bool l=true;

      // Если введён ключ вида /? или -? или /help или -help
      if (args.Length == 1) l = args[0].Length>9;

      if(File.Exists(fn)) {
        if(args.Length==0 || !l) {
          tx = File.ReadAllText(fn);
          k = tx.IndexOf("<"+cA,StringComparison.OrdinalIgnoreCase)+11;
          tx = tx.Substring(k, tx.IndexOf("</"+cA,StringComparison.OrdinalIgnoreCase)-k).
               Replace("\t", " ").Replace("\r"," ").Replace("\n"," ").Trim();
          k1 = k = 0;
          while (k<tx.Length) {
            i = tx.IndexOf(" ", k);
            if(i < 0) {
              k = tx.Length;
            } else {
              if(odd(tx.Substring(k1, i-k1)) == 0) {
                if(i>k) {
                  tx = tx.Substring(0, i)+"\t"+tx.Substring(i+1);
                } else {
                  tx = tx.Substring(0, i)+tx.Substring(i+1);
                  i--;
                }
                k1 = i + 1;
              }
              k = i + 1;
            }
          }
          args = tx.Split('\t');
          for (i = 0; i<args.Length; i++) {
            if (args[i].Length > 1) {
              if (args[i][0]==args[i][args[i].Length - 1]) {
                if (args[i][0]=='"' || args[i][0]=='\'')
                    args[i] = args[i].Substring(1, args[i].Length-2);
              }
            }
          }
        }
        tx = string.Empty;
      } else if(args.Length > 0) {

        bool hasSpace = Fullexe.Contains(' ');
        string quote = hasSpace ? "\"" : "";
        tx = $"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <BootTrigger>
      <Enabled>true</Enabled>
    </BootTrigger>
  </Triggers>
  <Principals>
    <Principal>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
  </Settings>
  <Actions>
    <Exec>
      <Command>{quote}{Fullexe}{quote}</Command>
      <Arguments></Arguments>
    </Exec>
  </Actions>
</Task>
""";
      }

      // Разбор параметров
      for (i = 0; i < args.Length; i++){
        switch (args[i]){
        case "-p":
          if(toArg(args)){
            k=valInt(args[i]);
            port= (k > 0 && k <= p9)? k : 0;
          }
          break;
        case "-p1":
          if(toArg(args)){
            k=valInt(args[i]);
            port1= (k > 0 && k <= p9)? k : 0;
          }
          break;
        case "-b":
          if(toArg(args)){
            k=valInt(args[i]);
            if(k<b0){
              bu=b0;
            }else{
              bu=(k <= b9)? k : b9;
            }
          }            
          break;
        case "-q":
          if(toArg(args)){
            k=valInt(args[i]);
            qu=(k > 0)? k : i9;
          }            
          break;
        case "-q1":
          if(toArg(args)) {
            k=valInt(args[i]);
            qu1= k > 0? k : 1;
          }
          break;
        case "-s":
          if(toArg(args)){
            k=valInt(args[i]);
            st= k>1? (k<=s9? k : s9) : 2;
          }            
          break;
        case "-s1":
          if(toArg(args)) {
            k=valInt(args[i]);
            st1= k > 0? k : 1;
          }
          break;
        case "-n":
          if(toArg(args)){
            k=valInt(args[i]);
            if(k >= 0 && k <= s9) it=k;
          }            
          break;
        case "-n1":
          if(toArg(args)){
            k=valInt(args[i]);
            if(k >= 0 && k <= s9) it1=k;
          }            
          break;
        case "-f":
          if(toArg(args)){
            k=valInt(args[i]);
            if(k >= 0 && k <= s9) db=k;
          }            
          break;
        case "-f1":
          if(toArg(args)){
            k=valInt(args[i]);
            if(k >= 0 && k <= s9) db1=k;
          }            
          break;
        case "-w":
          if(toArg(args)){
            k=valInt(args[i]);
            tw=((k > 0 && k <= t9)? k : t9)*1000;
          }            
          break;
        case "-log":
          if(toArg(args)){
            k=valInt(args[i]);
            log9=(k < log0)? 0 : k;
          }            
          break;
        case "-post":
          if(toArg(args)){
            k=valInt(args[i]);
            post=(k > 0)? k : post9;
          }            
          break;
        case "-d":
          if(toArg(args)) DocumentRoot=
            (args[i].EndsWith("/")||args[i].EndsWith("\\"))?args[i]:args[i]+"/";
          break;
        case "-i":
          if(toArg(args)) DirectoryIndex=args[i];
          break;
        case "-c":
          if(toArg(args)) CerFile=args[i];
          break;
        case "-proc":
          if(toArg(args)) Proc=args[i];
          break;
        case "-args":
          if(toArg(args)) Args=args[i];
          break;
        case "-ext":
          if(toArg(args)) Ext=args[i];
          break;
        case "/regserver":
          ts = "/create /tn "+hn+" /ru system /xml "+fn;
          if(tx.Length > 0) File.WriteAllText(fn,tx);
          i = args.Length;
          notQuit = false;
          break;
        case "/unregserver":
          ts = "/delete /f /tn \\"+hn;
          i = args.Length;
          notQuit = false;
          break;
        default:
          l=false;
          break;
        }
      }

      // Корректировка некоторых параметров
      k= st + 1000;
      if(qu<k) qu= k;

      if(ts.Length > 0) schtasks(ref ts);
      if(!Controls.Contains(textBox1)) {
        textBox1 = new TextBox()
        {
          Location = new Point(5,5),
          Size = new Size(this.ClientSize.Width-10,this.ClientSize.Height-10)
        };
        textBox1.TabStop = false;
        textBox1.ReadOnly = true;
        textBox1.WordWrap = true;
        textBox1.Multiline = true;
        textBox1.Font = new Font("Consolas", 13);
        textBox1.ScrollBars = ScrollBars.Vertical;
        Controls.Add(textBox1);
      }
      textBox1.Text = $"""
Multithreaded {hs} {ver}, (C) a.kornienko.ru {verD}.

USAGE:
    https.net [Parameter1 Value1] [Parameter2 Value2] ...
    https.net /regserver              Starting the server when the computer is turned on.
    https.net /unregserver            Deleting the server startup task.

    If necessary, Parameter and Value pairs are specified. If the value is text and contains
    spaces, then it must be enclosed in quotation marks. You can also specify the parameters
    string in the "{fn}" file in the <Arguments></Arguments> section.

Parameters:                                                                  Values:
     -d      Folder containing the domains.                                      {DocumentRoot}
     -i      Main document is in the folder. The main document in the            {DirectoryIndex}
             folder specified by the -d parameter is used to display the page
             with the 404 code - file was not found. To compress traffic,
             files compressed using gzip method of the name.expansion.gz type
             are supported, for example - index.html.gz or library.js.gz etc.
     -c      Name of the file containing the PFX certificate for the TLS 1.3     {CerFile}
             protocol without a password. If the path is not specified, the
             certificate is searched for in the folder where the https.net
             server is located and in the root folder containing the domains.
     -p      Port for https-connection. Zero to disable this connection.         {port}
     -p1     Port for http-connection. Zero to disable this connection.          {port1}
     -b      Size of read/write buffers.                                         {bu}
     -q      Allowable number of requests in the queue.                          {qu}
     -q1     Allowed number of requests in the queue per IP.                     {qu1}
     -s      Number of requests being processed at the same time. Maximum        {st}
             value is {s9}.
     -s1     Allowed number of simultaneously processed requests per IP.         {st1}
     -w      Allowed time to reserve an open channel for request that did not    {tw/1000}
             started. From 1 to {t9} seconds.
     -n      Maximum number of dynamically running interpreters. Processes       {it}
             are launched as needed depending on the number of concurrent
             requests. Maximum value is {s9}.
     -n1     The initial number of interpreters to pre-start.                    {it1}
     -f      Maximum number of dynamically launched FoxPro9.exe instances.       {db}
             FoxPro9.exe COMs are created as needed, depending on the number
             of concurrent requests. The maximum value is {s9}.
     -f1     The initial number of pre-created FoxPro9.exe COMs.                 {db1}
     -log    Size of the query log in rows. The log consists of two              {log9}
             interleaved versions https.net.x.log and https.net.y.log. If the
             size is set to less than {log0}, then the log is not kept.
     -post   Maximum size of the accepted request to transfer to the script      {post}
             file. If it is exceeded, the request is placed in a file,
             the name of which is passed to the script in the environment
             variable POST_FILENAME. Other generated environment variables -
             SERVER_PROTOCOL, SCRIPT_FILENAME, QUERY_STRING, REMOTE_ADDR. If
             the form-... directive is missing from the request data, then
             incoming data stream will be placed entirely in a file. This
             feature can be used to transfer files to the server. In this
             case, the file name will be in the environment variable
             POST_FILENAME.
     -proc   Script handler used. If necessary, you must also include            {Proc}
             the full path to the executable file.
     -args   Additional parameters of the handler startup command line.
     -ext    Extension of the script files.                                      {Ext}
""";
      return l;
    }
}
