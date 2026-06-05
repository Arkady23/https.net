//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!                                                         !!
//!!    https.net сервер на C#.      Автор: A.Б.Корниенко    !!
//!!    class Session                версия от 05.06.2026    !!
//!!                                                         !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

using System;
using System.IO;
using System.Web;
using System.Net;
using System.Text;
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace https2 {

  public class Session {
    int i, j, k, m, m1, i1, i2, k1, len, eof, Content_Length, n1, n2, nbuf;
    string h1, reso, res, head, Host, Content_Type, Content_T, IP, fullprg,
           Content_Disposition, QUERY_STRING, dirname, filename, Protocol,
           x1;
    Queue<string> heads = new Queue<string>();
    CancellationTokenSource readCts = new();
    CancellationTokenSource handCts = new();
    CancellationTokenSource exeCts = new();
    byte[] buf = new byte[F.bu];
    MemoryStream VFPstream;
    SslStream sslStream;
    FileStream file1;           // Файл для записи POST-данных
    IPEndPoint point;           // IP адрес клиента
    Stream stream;              // Объявляем объект как базовый Stream
    FileStream fs;              // Файл статического ресурса
    Task<bool> ts;              // Задача запуска обработчика
    Encoding UTF;
    DateTime dt1;
    ValueTask t;                // Задача задача ввода-вывода
    byte R, R1;                 // Однобайтовые флажки
    bool l, l1;                 // l=false, если заголовки прочитаны
    double n;                   // Количество мс в отметке времени
    long nf;                    // Длина посылаемого файла/потока

    public Session(int j) {
      dirname=filename= string.Empty;
      this.Init();
      this.j = j;
    }

    void Init() {

      // Подготовка переменных по максимуму
      if(filename.Length>F.i0) {
        if(Directory.Exists(dirname)) Directory.Delete(dirname,true);
        dirname=filename= string.Empty;
      }

      // Если клиентов много, то сбрасываем счетчики DoS-атак, только если другой IP.
      // А если клиентов больше нет, то сбрасывает счетчик DoS-атаки F.iIP1.
      if(F.nClients>F.i1) {
        Interlocked.Decrement(ref F.nClients);
        if(F.IP != IP) {
          Interlocked.Exchange(ref F.iIP,F.i0);
          Interlocked.Exchange(ref F.iIP1,F.i0);
        }
      } else {
        if(F.IP != IP) Interlocked.Exchange(ref F.iIP,F.i0);
        Interlocked.Exchange(ref F.nClients,F.i0);
        Interlocked.Exchange(ref F.iIP1,F.i0);
      }

      head=h1=res=reso=Host=Content_T=Content_Type=Content_Disposition=QUERY_STRING=string.Empty;
      VFPstream?.Close(); VFPstream?.Dispose();
      eof = len = i2 = Content_Length = F.i0;
      UTF = Encoding.GetEncoding(F.UTF8);
      VFPstream = new MemoryStream();
      fs = file1 = null;                    // Освободить объекты
      F.IP = F.IP1 = IP;                    // Предыдущий IP для сравнения на выходе и входе
      k1 = n2 = F.bu3;                      // Смещение для чтения 2-ой части заголовков
      k = n1 = F.bu1;                       // Смещение для чтения 1-ой части заголовков
      heads.Clear();                        // Очистка блока заголовков
      R = R1 = F.b0;                        // Однобайтовые флажки
      nbuf = F.bu4;                         // Число читаемых за один раз в заголовках байтов
      ts = null;                            // Задача запуска оброботчика
      l = true;                             // Заголовок пока не прочитан
    }

    // Увеличить счетчик IP с подозрительными запросами
    void fIP() {
      if(F.IP==IP) {
        Interlocked.Increment(ref F.iIP);
        if(F.iIP>F.i2) res+="-";
      }
    }

    public async Task Start(Socket client, string Prot) {
      client.NoDelay = true;
      point = client.RemoteEndPoint as IPEndPoint;
      IP = point.Address.ToString();
      Protocol = Prot;
      if((F.iIP>F.st1 && F.IP==IP) || (F.iIP1>F.qu1 && F.IP1==IP)) {
        client.Close();
        F.log2($"/0000 {IP}\t IP blocked.");
      } else {
        Interlocked.Increment(ref F.nClients);
        if(F.IP1==IP) Interlocked.Increment(ref F.iIP1);

        if(Protocol=="https") {

          // Гарантированно оживляем источник токенов
          if(handCts == null || !handCts.TryReset()) {
            handCts?.Dispose();
            handCts = new CancellationTokenSource();
          }
          handCts.CancelAfter(F.tw);

          try{
            sslStream = new SslStream(new NetworkStream(client, true), false);
            await sslStream.AuthenticateAsServerAsync(F.cert, handCts.Token);
            stream = sslStream;
          }catch (OperationCanceledException){
            stream?.Dispose();
            stream = null;
            fIP();
          }catch(Exception){
            stream?.Dispose();
            stream = null;
          }
        }else{
          try {
            stream = new NetworkStream(client,true);
          } catch(Exception) {
            stream?.Dispose();
            stream = null;
          }
        }
        if(stream != null) {
          if(F.IP1==IP) Interlocked.Decrement(ref F.iIP1);

          while (l) {
            await sReadAsync();      // Читаем асинхронно
            if(i>F.i0) {
              len += i;
              getHeaders();
            } else {
              l = false;
            }
          }

          if(eof<F.i0) {
            stream.Close();
            client.Close();
            Init();
            return;
          }

          // Заголовки прочитали, фомируем ответ
          if(R>F.b0) {
            n1 = F.i0;
            n2 = F.bu2;
            nbuf = F.bu8;
            if(R>F.b1) {
              putHead(true);
              if(R1>F.b0 || File.Exists(res)) {
                x1 = F.valStr(ref Content_Type,"charset");
                if(x1.Length>F.i0 && !String.Equals(x1,F.UTF8,
                      StringComparison.CurrentCultureIgnoreCase)) {
                  try { UTF = Encoding.GetEncoding(x1); } catch(Exception) { }
                }
                if(R==F.b2) {
                  await send_cgi();
                } else {
                  await send_prg();
                }
              }
            } else {
              if(!gzExists(true)) {
                if(File.Exists(res)) {
                  putHead(true);
                } else {
                  res = F.DocumentRoot+F.DI;
                  if(!gzExists(false)) {
                    putHead(false);
                    if(!File.Exists(res)) {
                      R = F.b0;
                      await failure("404 Not Found");
                    }
                  }
                }
              }
              if(R==F.b1) await typeAsync();
            }
          } else {
            if(res.Length>F.i0) {
              res+=" -";
              await failure("403 Forbidden");

              // На первый раз пропускаем, но счетчик у этого IP увеличиваем.
              fIP();

            }
          }
          stream?.Close();
        }
        client.Close();

        if(res.Length>F.i1 && F.log9>F.i0) {
          n = DateTime.UtcNow.Subtract(dt1).TotalMilliseconds;
          string nStr= n > 9999 ? "****" : $"{n:0000}";
          string jfm = string.Format(F.itf, j);
          if(R>F.b1) F.log2($"/{nStr} {IP}{jfm}/{m}\t{res}");
          else       F.log2($"/{nStr} {IP}{jfm}  \t{res}");
        }
        Init();
      }
    }

    void putHead(bool CT) {
      // CT - true, тип контента не изменяется
      //      false, тип контента стал html.
      head="Date: "+dt1.ToString("R")+"\r\n"+h1+
           (CT? Content_T : F.CT+": text/html\r\n");
    }

    void putCT(ref string c, string x) {
      c = F.CT+": "+x+"\r\n";
      h1 = F.CC;
    }

    bool gzExists(bool CT) {
      string gz=res+".gz";
      bool l = File.Exists(gz);
      if( l ) {
        res = gz;
        putHead(CT);
        head += "Content-Encoding: gzip\r\n";
      }
      return l;
    }

    string line1() {
      string z = string.Empty;
      if(len>F.i0) {
        i = Array.IndexOf(buf,F.b10,k,len);
        if(i >= F.i0) {
          if(i>F.i0 && buf[i-F.i1]==F.b13) {
            m1 = i-k-F.i1;
            len -= m1+F.i2;
          } else {
            m1 = i-k;
            len -= m1+F.i1;
          }
          z += UTF.GetString(buf,k,m1);
          k = i+F.i1;
        }
      }
      l = z.Length>F.i0;
      return z;
    }

    void getHeaders() {
      string lin,z,h;
      do {
        lin = line1();
// F.log2(" "+lin);
        h = F.afterStr1(ref lin,":");
        h = F.ltri(ref h);
        if(h.Length>F.i0) {
          z = F.beforStr1(ref lin,":");
          switch(z) {
          case "Host":
            Host = h;
            prepResource();
            switch (R) {
            case F.b0:
            case F.b1:
              l = false;  // Дальше читать бессмысленно
              break;
            case F.b2:
              m = -F.i1;
              if(F.cgia && F.freeCGI.TryPop(out m)) {
                if(F.cgib[m]==F.b0) {
                  l1=true;
                } else {
                  try {
                    l1 = F.proc[m] == null || F.proc[m].HasExited;
                  } catch(Exception) {
                    l1 = true;
                  }
                }
                if(l1) ts = Task.Run(() => F.start_CGI(m));
              }
              break;
            case F.b3:
              m = -F.i1;
              if(F.vfpa != null && F.freeVFP.TryPop(out m)) {
                if(F.vfpb[m]==F.b0) {
                  l1=true;
                } else {
                  try {
                    _= F.vfp[m].ProcessID;
                    l1=false;
                  } catch(Exception) {
                    l1=true;
                  }
                }
                if(l1) ts = Task.Run(() => F.start_VFP(m));
              }
              break;
            }
            break;
          case F.CT:
            Content_Type = h;
            break;
          case F.CD:
            Content_Disposition = h;
            break;
          case F.CL:
            try { Content_Length = int.Parse(h); } catch(Exception) { Content_Length = F.i0; }
            break;
          }
          heads.Enqueue(z);
          heads.Enqueue(h);
        } else {
          i = lin.IndexOf(" ");
          if(i > F.i0) {
            z = lin.Substring(F.i0,i);
            if(z=="GET" || z=="POST" || z=="PUT") {
              h = lin.Substring(i+F.i1);
              h = F.ltri(ref h);
              i = h.IndexOf(" ");
              if(i > F.i0) reso = h.Substring(F.i0,i);
            }
          }
        }
      } while(l);

      // Перенести остаток байт заголовочной части из bu2 в конец bu1
      if(R>F.b1) {
        i = k;
        k = k1-len;
        Array.Copy(buf, i, buf, k, len);
      }
    }

    void prepResource() {
      string sub,ext = ".";
      if(reso.Length==F.i0) {
        R=F.b0;
      } else {
        res = HttpUtility.UrlDecode(reso);
        QUERY_STRING = F.afterStr1(ref res,"?");
        res = F.beforStr1(ref res,"?");
        sub = F.beforStr1(ref Host,":");

        // ".." в запроах недопустимы в целях безопасности
        if(res.IndexOf("..")<F.i0){

          if(res.EndsWith("/")) res += F.DirectoryIndex;
          reso = F.afterStr9(ref res,"/");
          ext = F.afterStr9(ref reso,ext);
          if(ext.Length==F.i0){
            reso = F.DocumentRoot+sub+res+".";
            if(File.Exists(reso+F.Ext)) {
              R1 = F.b1;      // Случай API
              ext = F.Ext;
              res += "."+ext;
            } else if(File.Exists(reso+"prg")) {
              R1 = F.b1;      // Случай API
              ext = "prg";
            } else if(Directory.Exists(reso)) {
              res += "/"+F.DirectoryIndex;
              ext = F.afterStr9(ref F.DirectoryIndex,".");
            } else if(! File.Exists(reso)) {
              ext = "html";
              res += "."+ext;
            }
          }
        }
        R = F.b1;
        switch(ext) {
        case "html":
          putCT(ref Content_T,"text/html");
          break;
        case "svg":
          putCT(ref Content_T,"image/svg+xml");
          break;
        case "gif":
          putCT(ref Content_T,"image/gif");
          break;
        case "png":
          putCT(ref Content_T,"image/png");
          break;
        case "jpeg":
        case "jpg":
          putCT(ref Content_T,"image/jpeg");
          break;
        case "js":
          putCT(ref Content_T,"text/javascript");
          break;
        case "css":
          putCT(ref Content_T,"text/css");
          break;
        case "ico":
          putCT(ref Content_T,"image/x-icon");
          break;
        case "mp4":
          putCT(ref Content_T,"video/mp4");
          break;
        case "txt":
        case "":
          Content_T = F.CT_T;
          break;
        default:
          if(ext==F.Ext) {
            R = F.b2;
          } else if(ext=="prg") {
            R = F.b3;
          } else {
            // Все другие расширения недопустимы в целях безопасности
            R = F.b0;
          }
          break;
        }
        reso = sub+res;
        res = F.DocumentRoot+reso;
      }
    }

    async Task failure(string s) {
      string z = F.H1+s+"\r\n";
      i = UTF.GetBytes(z,F.i0,z.Length,buf,F.i0);
      await stream.WriteAsync(buf.AsMemory(F.i0,i));
    }

    // Запись данных POST aсинхронно*
    ValueTask sWriteAsync(byte b, ReadOnlyMemory<byte> data) {
      switch(b) {
      case F.b2:
        return F.proc[m].StandardInput.BaseStream.WriteAsync(data);
      case F.b3:
        return VFPstream.WriteAsync(data);
      default:
        return file1.WriteAsync(data);
      }
    }

    // Асинхронное Чтение данных в половинку буфера
    async Task sReadAsync() {

      // Гарантированно оживляем источник токенов
      if (readCts == null || !readCts.TryReset()) {
         readCts?.Dispose();
         readCts = new CancellationTokenSource();
      }
      readCts.CancelAfter(F.tw);

      k1 = k1 < F.bu2 ? n2 : n1; // чередуем буферы в половинках

      try {
        i = await stream.ReadAsync(buf.AsMemory(k1, nbuf), readCts.Token);

        // Дополнительная проверка на конец потока (EOF)
        if(i > F.i0) {
          if(eof == F.i0) {
            dt1 = DateTime.UtcNow;
            eof = F.i1;   // Обычное чтение
          }
        } else {
          eof = F.i3;     // Конец потока
          l = false;
        }
      } catch(OperationCanceledException) {

        // Сюда мы гарантированно прилетим при таймауте F.tw, 
        // при этом фоновая задача в ОС гарантированно УНИЧТОЖИТСЯ
        i=eof= -F.i1;     // Таймаут приравнивается сетевой ошибке

      } catch(Exception) {
        i=eof= -F.i1;     // Сетевая ошибка
        l = false;
      }
    }

    // Отправка файла
    async Task typeAsync(){
      head = F.OK+head+F.CL+": ";
      fs = File.OpenRead(res);
      nf = fs.Length;
      head += nf+"\r\n\r\n";
      i = UTF.GetBytes(head, F.i0, head.Length, buf, n1);
      i2 = await fs.ReadAsync(buf.AsMemory(i, nbuf-i)); // Заполнить первую половину буфера синхронно
      t = stream.WriteAsync(buf.AsMemory(n1, i2+i));    // Асинхронно записать в поток
      k = n2;
      while (i2<nf) {
        i = await fs.ReadAsync(buf.AsMemory(k, nbuf));  // Синхронно прочитать
        if(i>F.i0) {
          await t;
          t = stream.WriteAsync(buf.AsMemory(k, i));
          k = k==n1? n2 : n1;
          i2 += i;
        } else {
          i2 = (int)nf;
        }
      }
      await t;
      fs.Close();
    }

    bool filename2(){
      filename=F.valStr(ref Content_Disposition,"filename");
      if(filename.Length>F.i0 || Content_Length>(R==F.b2?F.post:F.maxVFP)){
        dirname=F.DirectorySessions+"/"+IP+"_"+point.Port.ToString();
        if(filename.Length==F.i0) filename=DateTime.Now.ToString("HHmmssfff");
        filename = dirname+"/"+HttpUtility.UrlDecode(filename);
        return true;
      }
      return false;
    }

    // Передаем блок заголовков
    void res_start(){
      reso = res+"\nSCRIPT_FILENAME:"+F.fullres(ref res)+"\nQUERY_STRING:"+
             QUERY_STRING+"\nREMOTE_ADDR:"+IP+"\nSERVER_PROTOCOL:"+Protocol;
      while (heads.Count>F.i1) reso += "\n"+heads.Dequeue()+":"+heads.Dequeue();
      F.proc[m].StandardInput.WriteLine(reso.Length.ToString()+"\n"+reso);
    }

    // Передача данных из потока в объект
    async Task send_stream(byte b) {
      if(len<Content_Length && eof==F.i1) {
        l = true;
        while (l) {

          // Читаем асинхронно
          await sReadAsync();

          if(i>F.i0) {
            i += len;
            i2 += i;
            await sWriteAsync(b, buf.AsMemory(k,i));
            l = i2<Content_Length;
            len = F.i0;
            k = k1;
          } else {
            l = false;
          }
        }
      } else {
        i = len;
        await sWriteAsync(b, buf.AsMemory(k,i));
      }
    }

    // Чтение файла из трафика
    async Task send_file() {

      // Открыть файл, если он не открыт
      if (File.Exists(filename)) {
        File.Delete(filename);
      } else if(!Directory.Exists(dirname)) {
        Directory.CreateDirectory(dirname);
      }
      file1 = new FileStream(filename,FileMode.Create);
      await send_stream(F.i0);
      if(file1.CanRead) file1.Close();
    }

    async Task send_cgi() {
      fullprg = F.fullres(ref res);
      if(m < F.i0) {

        // Вывести сообщение об отсутствии интерпретатора
        await send_txtAsync($"There is no \"{F.Proc}\" on the server :(");
        return;
      }

      try{
        if(ts != null && await ts) m = F.db;
      } catch(Exception) {
        m = F.db;
      }
      if(m >= F.db) {

        // Вывести сообщение, что все доступные процессы интерпретатора заняты
        await send_txtAsync($"All {F.db} \"{F.Proc}\" processes are busy :(");
        return;
      }

      // Чтение данных POST
      heads.Enqueue("POST_FILENAME");
      if(filename2()) {

        // Если в потоке файл
        heads.Enqueue(F.Folder+filename);
        await send_file();
        res_start();

      } else {

        // и если просто поток
        heads.Enqueue(filename);
        res_start();
        await send_stream(R);
      }
      F.proc[m].StandardInput.Close();
      if(eof>F.i0) {      // Если нет разрыва связи
        if(exeCts == null || !exeCts.TryReset()) {
          exeCts?.Dispose();
          exeCts = new CancellationTokenSource();
        }
        exeCts.CancelAfter(F.i8);
        using var registration = exeCts.Token.Register(() => {
          try {
            if(F.proc[m] != null && !F.proc[m].HasExited) F.proc[m].Kill();
          } catch { }
        });

        // Вывод полученных данных cgi-скрипта
        reso = F.OK+head;

        // Помещаем заголовок в буфер с позиции n2
        k = UTF.GetBytes(reso, F.i0, reso.Length, buf, n2);

        i1 = nbuf-k;    // До конца буфера осталось

        string cErr= string.Empty;
        try {

          // Прочитать i1 символов в buf начиная с n2+k
          k1 = n2+k;
          i1 = await F.proc[m].StandardOutput.BaseStream.ReadAsync(buf.AsMemory(k1, i1),
                     exeCts.Token);

          // Проверить код возврата
          if(R1>F.b0) {
            i=F.valInt(UTF.GetString(buf, k1, F.i4));
            if(i>=100 && i<=599) {
               i = Array.IndexOf(buf, F.b10, k1, i1);
               if(i>k1) {
                  i++;
                  reso = F.H1+UTF.GetString(buf,k1,i-k1)+head;
                  k1 = i-UTF.GetByteCount(reso);
                  UTF.GetBytes(reso,F.i0,reso.Length,buf,k1);
                  i1 += n2-k1;
               } else {
                  k1 = n2;
               }
            } else {
              k1 = n2;
            }
          } else {
            k1 = n2;
          }

          i1 += k;
          while (i1>F.i0) {
            t = stream.WriteAsync(buf.AsMemory(k1, i1));  // Асинхронно записать в поток
            k1 = k1<n2? n2 : n1;                          // Следующее начало буфера
            i1 = await F.proc[m].StandardOutput.BaseStream.ReadAsync(buf.AsMemory(k1, nbuf),
                 exeCts.Token);
            await t;
          }
        } catch (OperationCanceledException) {
          cErr= $"\r\n\r\nError in Pyton.exe: The maximum calculation duration of {
                   F.i8} ms has been exceeded.";
        } catch (Exception e) {
          cErr= $"\r\n\r\nError in Python: {e.Message}";
        } finally {
          if(cErr.Length > F.i0) try { await stream.WriteAsync(UTF.GetBytes(cErr));
                                 } catch { }
          clear_cgi(m);
        }
      } else {
        clear_cgi(m);
      }
    }

    // Вывод текстового сообщения длиной до 1 буфера
    ValueTask send_txtAsync(string s) {
      string z = F.OK+head+F.CT_T+"\r\n"+s;
      i = UTF.GetBytes(z,F.i0,z.Length,buf,F.i0);
      return stream.WriteAsync(buf.AsMemory(F.i0,i));
    }

    async Task send_prg() {
      //prg=F.afterStr9(ref res,"/");
      fullprg=F.fullres(ref res);
      if(m < F.i0) {

        // Вывести сообщение об отсутствии VFP в реестре
        await send_txtAsync("\"vfoxpro.Engine\" is missing in the Windows registry :(");
        return;
      }

      if(ts != null && await ts) m = F.db;
      if(m >= F.db) {
        // Вывести сообщение, что все процессы VFP заняты
        await send_txtAsync($"All {F.db.ToString()} VFoxPro.exe processes are busy :(");
        return;

      } else {
        if(filename2()) {      // Определяем и проверяем наличие имя файла для данных
          F.vfp[m].POST_FILENAME= F.Folder+filename;
          await send_file();   // Записываем в файл
        } else {
          F.vfp[m].POST_FILENAME= filename;
          await send_stream(R);
        }
        if(eof < F.i0) {       // Если обнаружен разрыв связи
          clear_prg(m);
          return;
        }
      }
      F.vfp[m].REMOTE_ADDR= IP;
      F.vfp[m].SCRIPT_FILENAME= fullprg;
      F.vfp[m].SERVER_PROTOCOL= Protocol;
      F.vfp[m].QUERY_STRING= QUERY_STRING;
      F.vfp[m].STD_INPUT= VFPstream.Length > F.i0? VFPstream.ToArray() : new byte[0];
      while (heads.Count>F.i1) F.vfp[m].SetVar(heads.Dequeue().Replace("-","_"),
             heads.Dequeue());
      if (exeCts == null || !exeCts.TryReset()) {
         exeCts?.Dispose();
         exeCts = new CancellationTokenSource();
      }
      // Если выполнение prg не закончилось за 25 минут, то аварийно снять процесс
      exeCts.CancelAfter(F.i8);

      // Вывод полученных данных prg-скрипта
      string sAll;
      try{
        var api= Task.Run(() => F.vfp[m].Eval());
        var ret = await api.WaitAsync(exeCts.Token);
        if(ret.GetType().Name=="String" && ret.Length>5) {
           i = F.valInt(ret.Substring(F.i0,F.i4));
           if(i>=100 && i<=599) head = $"{F.H1}{ret}\r\n";
        } else {
          head = string.Concat(F.OK,head);
        }
        sAll= string.Concat(head, F.vfp[m].STD_OUTPUT);
      } catch(OperationCanceledException){
        F.killVFP(m);
        sAll= $"{F.OK}{head}{F.CT_T}\r\nError in VFoxPro.exe: The maximum calculation duration of {
                 F.i8} ms has been exceeded.";
        Content_Length = F.i0;
      } catch(Exception e){
        sAll= $"{F.OK}{head}{F.CT_T}\r\nError in VFoxPro.exe: {e.Message}";
        Content_Length = F.i0;
      } finally {
        clear_prg(m);
      }
      byte[] buff = ArrayPool<byte>.Shared.Rent(sAll.Length);
      i1= sAll.Length>33554432? sAll.Length/F.i3*F.i2 + F.i1 : sAll.Length;
      try {
        F.vfpw.GetBytes(sAll, F.i0, i1, buff, F.i0);
        t= stream.WriteAsync(buff.AsMemory(F.i0, i1));
        if(i1 == sAll.Length) {
          await t;
        } else {
          i2= sAll.Length - i1;
          F.vfpw.GetBytes(sAll, i1, i2, buff, i1);
          await t;
          await stream.WriteAsync(buff.AsMemory(i1, i2));
        }
      } finally {
        ArrayPool<byte>.Shared.Return(buff);
      }
    }

    void clear_cgi(int m) {
      _= Task.Run(() => F.clear_cgi(m));
    }

    void clear_prg(int m) {
      _= Task.Run(() => F.clear_prg(m));
    }

  }
}
