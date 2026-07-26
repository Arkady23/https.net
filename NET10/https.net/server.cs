//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!                                                     !!
//!!   https.net сервер на C#.  Авторы: A.Б. Корниенко   !!
//!!                                    И.И.google.com   !!
//!!   Серверный движок         версия  от  28.06.2026   !!
//!!                                                     !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

using System;
using System.Net;
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace https1 {

  class Server {
    ConcurrentStack<int> freeClientsPool;
    Socket listenSocket, listenSocket1;
    SemaphoreSlim poolSemaphore;

    public bool Start(IPEndPoint ep, IPEndPoint ep1) {
      listenSocket1 = CreateListenSocket(ep1, in F.port1);
      listenSocket = CreateListenSocket(ep, in F.port);
      if(listenSocket1 == null) F.port1 = 0;
      if(listenSocket == null) F.port = 0;
      if(F.port==0 && F.port1==0) {
         return false;
      } else {

        // Запуск чтения сокетов
        if(F.port>0 || F.port1>0) {
          poolSemaphore = new SemaphoreSlim(F.st,F.st);
          freeClientsPool = new ConcurrentStack<int>();
          for (int i=F.st; i>0; i--) freeClientsPool.Push(i);
          if(F.port>0) _= Task.Run(() => httpsAcceptAsync());
          if(F.port1>0) _= Task.Run(() => httpAcceptAsync());
        }

        //Console.WriteLine("Press any key to terminate the server process....");
        //Console.ReadKey();

        return true;
      }
    }

    Socket CreateListenSocket(IPEndPoint ep, in int port) {
      Socket s = null;
      if(port>0) {
        // create the socket which listens for incoming connections
        s = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { 
                NoDelay = true,             // Мгновенная отправка
                DualMode = true             // Также принимать IPv4
        };

        // КРИТИЧЕСКИ ВАЖНО ДЛЯ ВЫСОКОЙ НАГРУЗКИ И ТЕСТОВ F5:
        // Разрешаем операционной системе мгновенно переиспользовать порт и адрес,
        // игнорируя системные задержки TIME_WAIT от предыдущих запросов браузера.
        s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try { s.Bind(ep); } catch (Exception) { s.Close(); s = null; }

        // start the server with a listen backlog of F.qu connections
        s?.Listen(F.qu);
      }
      return s;
    }

    // Головной модуль запуска задачи https-сервера
    public async Task httpsAcceptAsync() {
      Socket client;
      while (F.notExit) {
        try { client = await listenSocket.AcceptAsync(); }
        catch (ObjectDisposedException) { break; }
        _ = toSession(client, true);
      }
    }

    // Головной модуль запуска задачи http-сервера
    public async Task httpAcceptAsync() {
      Socket client;
      while (F.notExit) {
        try { client = await listenSocket1.AcceptAsync(); }
        catch (ObjectDisposedException) { break; }
        _ = toSession(client, false);
      }
    }

    async Task toSession(Socket s, bool Prot) {

      // Ждем освобождения места в пуле асинхронно (без блокировки потока!)
      if (await poolSemaphore.WaitAsync(F.tw)) {

         try {
           if(freeClientsPool.TryPop(out int j)) {
              await F.session[j].Start(s, Prot);
              freeClientsPool.Push(j);
           } else {
             s.Close();
           }
         } catch {
           s.Close();
         } finally {
           poolSemaphore.Release();
         }
      } else {

        // Если какая-нибудь сессия не освободилось за время F.tw.
        try {
          await s.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(
               "HTTP/1.1 503 Service Unavailable\r\n")), SocketFlags.None);
        } catch { }

        s.Close();
        char[] rentBuffer = ArrayPool<char>.Shared.Rent(256);
        if(rentBuffer.AsSpan().TryWrite($"\tQueue timeout. The number of running tasks via {
                                        Prot} exceeded {F.st}.", out int charsWritten)) {
          F.log2(rentBuffer.AsMemory(0, charsWritten));
        } else {
          ArrayPool<char>.Shared.Return(rentBuffer);
        }
      }
    }

    // Остановить сервер
    public void Stop() {
       // Закрыть прослушивание
       CloseSocket(listenSocket1, in F.port1);
       CloseSocket(listenSocket, in F.port);
       poolSemaphore?.Dispose();
    }

    void CloseSocket(Socket s, in int port) {
      if(port>0 && s != null) s.Close();
    }
  }
}
