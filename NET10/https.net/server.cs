//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!                                                      !!
//!!   https.net сервер на C#.  Авторы: A.Б. Корниенко    !!
//!!                                    ИИ от google.com  !!
//!!   Серверный движок         версия  от 26.05.2026     !!
//!!                                                      !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace https1 {

  class Server {
    ConcurrentStack<int> freeClientsPool;
    Socket listenSocket, listenSocket1;
    Task tSer, tSer1;

    public bool Start(IPEndPoint ep, IPEndPoint ep1) {
      listenSocket1 = CreateListenSocket(ep1, in F.port1);
      listenSocket = CreateListenSocket(ep, in F.port);
      if(listenSocket1 == null) F.port1 = F.i0;
      if(listenSocket == null) F.port = F.i0;
      if(F.port==F.i0 && F.port1==F.i0) {
         return false;
      } else {

        // Запуск чтения сокетов
        if(F.port>F.i0 || F.port1>F.i0) {
          freeClientsPool = new ConcurrentStack<int>();
          for (int i=F.st; i>F.i0; i--) freeClientsPool.Push(i);
          if(F.port>F.i0) tSer = Task.Run(() => httpsAcceptAsync());
          if(F.port1>F.i0) tSer1 = Task.Run(() => httpAcceptAsync());
        }

        //Console.WriteLine("Press any key to terminate the server process....");
        //Console.ReadKey();

        return true;
      }
    }

    Socket CreateListenSocket(IPEndPoint ep, in int port) {
      Socket s = null;
      if(port>F.i0) {
        // create the socket which listens for incoming connections
        s = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp) {
                NoDelay = true };            // Мгновенная отправка

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
        _ = Task.Run(() => toSession(client, F.https));
      }
    }

    // Головной модуль запуска задачи http-сервера
    public async Task httpAcceptAsync() {
      Socket client;
      while (F.notExit) {
        try { client = await listenSocket1.AcceptAsync(); }
        catch (ObjectDisposedException) { break; }
        _ = Task.Run(() => toSession(client, F.http));
      }
    }

    async Task toSession(Socket s, string Prot) {
      if (s == null) return;

      // Отсекаем мертвые души сразу
      if (s.Poll(F.i0,SelectMode.SelectRead) && s.Available == F.i0) {
         s.Close();
         return; 
      }

      if(freeClientsPool.TryPop(out int j)) {
         try {
           await F.session[j].Start(s, Prot);
         }
         finally {
           s.Close();
           freeClientsPool.Push(j);
         }
      } else {
         s.Close();
         F.log2($"\tThe number of running tasks via {Prot} has exceeded the allowed value of {F.st}.");
      }
    }

    // Остановить сервер
    public void Stop() {
       // Закрыть прослушивание
       CloseSocket(listenSocket1, in F.port1);
       CloseSocket(listenSocket, in F.port);
    }

    void CloseSocket(Socket s, in int port) {
      if(port>0 && s != null) s.Close();
    }

  }
}
