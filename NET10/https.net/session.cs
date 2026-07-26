//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!                                                         !!
//!!    https.net сервер на C#.      Автор: A.Б.Корниенко    !!
//!!    class Session                версия от 26.07.2026    !!
//!!                                                         !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

using System;
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using System.Buffers.Text;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace https2 {

  public class Session {
    static ReadOnlySpan<byte> GetCT(int id) => id switch {
        0 => "Content-Type: text/html\r\n"u8,
        1 => "Content-Type: image/svg+xml\r\n"u8,
        2 => "Content-Type: image/gif\r\n"u8,
        3 => "Content-Type: image/png\r\n"u8,
        4 => "Content-Type: image/jpeg\r\n"u8,
        5 => "Content-Type: text/javascript\r\n"u8,
        6 => "Content-Type: text/css\r\n"u8,
        7 => "Content-Type: image/x-icon\r\n"u8,
        8 => "Content-Type: video/mp4\r\n"u8,
        _ => "Content-Type: text/plain\r\n"u8 // Значение по умолчанию (если id неверный)
    };
    static ReadOnlySpan<byte> CRLF => "\r\n"u8;
    static ReadOnlySpan<byte> H1 => "HTTP/1.1 "u8;
    static ReadOnlySpan<byte> CL=> "Content-Length"u8;
    static ReadOnlySpan<byte> TrailingGarbage1 => " \t"u8;
    static ReadOnlySpan<byte> OK => "HTTP/1.1 200 OK\r\n"u8;
    static ReadOnlySpan<byte> TrailingGarbage2 => " \t\""u8;
    static ReadOnlySpan<byte> TrailingGarbage3 => " \t\r"u8;
    static ReadOnlySpan<byte> TrailingGarbage  => ";\"\r\t "u8;
    static ReadOnlySpan<char> TrailingGarbageChars => ";\"\r\t ";
    static ReadOnlySpan<byte> Gzip => "Content-Encoding: gzip\r\n"u8;
    static ReadOnlySpan<byte> CC=> "Cache-Control: public, max-age=2300000\r\n"u8;
    int i, j, k, m, i1, k1, i7, Content_T, eof, nbuf, parsedPosition, headersCount, headersLen,
         totalBytesRead, firstLine, nres, ifilename, ifilename2, nfilename, ndirname,
         iQUERY_STRING, nQUERY_STRING, iCharset, nCharset;
    bool l, l1, needHost, needContentType, needContentDisposition, needLastLine,
         isCC, isCT, isDate, isGzip, isHttps;
    CancellationTokenSource readCts = new();
    CancellationTokenSource handCts = new();
    CancellationTokenSource exeCts = new();
    long Actual_Length, Content_Length;
    UnmanagedMemoryStream VFPstream;
    char[] dirname = new char[256];
    byte[] buf = new byte[F.bu];
    char[] res = new char[256];
    const byte b10 = 10;
    SslStream sslStream;
    FileStream file1;           // Файл для записи POST-данных
    IPEndPoint point;           // IP адрес клиента
    Encoding stdEnc;            // Кодировка в stdInput
    Stream stdInput;            // Для ссылки на запись в StandardInput (CGI)
    Stream stream;              // Объявляем объект как базовый Stream
    Task<bool> ts;              // Задача запуска обработчика
    IPAddress IP;
    Encoding UTF;
    DateTime dt1;
    string j_fmt;
    double n;                   // Количество мс в отметке времени
    byte R;                     // Однобайтовые флажки

    public Session(int j) {
      j_fmt = j.ToString().PadLeft(F.stf);
      IP = IPAddress.None;
      nfilename = 0;
      this.j = j;
      Init();
    }

    void Init() {

      // Подготовка переменных по максимуму
      if(nfilename > 0) {
        var dirInfo = new DirectoryInfo(new string(dirname, 0, ndirname));
        if (dirInfo.Exists) dirInfo.Delete(true);
        ndirname = 0;
      }
      nres = nQUERY_STRING = firstLine = 0;
      isCT = isDate = isGzip = false;
      F.DecrIP(IP);                         // Разпешить еще один запрос
      file1 = null;                         // Освободить объекты
      isCC = true;                          // Кеширование надо
      ts = null;                            // Задача запуска оброботчика
      R = 0;                                // Однобайтовые флажки

      InitStream();
    }

    // Приготовить данные для входного потока
    void InitStream() {
      Content_T = eof = totalBytesRead = parsedPosition = headersCount = nCharset = 0;
      l = needContentType = needContentDisposition = needHost = needLastLine = true;
      nbuf = F.HeaderBufSize;               // Число читаемых за один раз в заголовках байтов
      Content_Length = 0;                   // Длина POST
      headersLen = -1;                      // Длина блока заголовков без последнего '\n'
      nfilename = 0;
      UTF = F.UTF8;                         // Кодировка по умолчанию
    }

    public async Task Start(Socket client, bool Prot) {
      client.NoDelay = true;
      point = client.RemoteEndPoint as IPEndPoint;
      IP = point.Address.IsIPv4MappedToIPv6 ? point.Address.MapToIPv4() : point.Address;
      l1 = F.ifIP(IP);
      if(l1 && (F.iIP>=F.st1 || F.iIP1>=F.qu1 )) {
        client.Close();
        if(F.iIP==F.st1 || F.iIP1==F.qu1) {
          char[] rentBuffer = ArrayPool<char>.Shared.Rent(64);
          char cProt = Prot ? '/' : '|';
          if(IP.AddressFamily == AddressFamily.InterNetwork ?
                rentBuffer.AsSpan().TryWrite(
                           $"{cProt}0000 {IP,-15}{j_fmt}  \tIP blocked.", out i) :
                rentBuffer.AsSpan().TryWrite(
                           $"{cProt}0000 {IP}{j_fmt}  \tIP blocked.", out i))
          {
            F.log2(rentBuffer.AsMemory(0, i));
          } else {
            ArrayPool<char>.Shared.Return(rentBuffer);
          }
          Interlocked.Increment(ref F.iIP1);
          Interlocked.Increment(ref F.iIP);
        }
      } else {
        if(l1) {
          Interlocked.Increment(ref F.iIP1);
          Interlocked.Increment(ref F.iIP);
        } else {
          F.IP = IP;
        }
        isHttps = Prot;
        if(Prot) {
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
            F.DecrIP1(IP);          // Человек не виноват за обозреватель интернет
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
          F.DecrIP1(IP);

          // Читаем заголовки из потока
          await getHeadersAsync(stream).ConfigureAwait(false);

          if(eof<0) {
            stream.Close();
            client.Close();
            Init();
            return;
          }

          // Заголовки прочитали, фомируем ответ
          if(R>0) {
            nbuf = F.bu;
            if(R>1) {
              putHead(false);
              if(CheckFile()) {
                if(nCharset>0 && !Ascii.EqualsIgnoreCase(buf.AsSpan(iCharset, nCharset),
                                 "utf-8"u8)) {
                  try {
                     string CharS = UTF.GetString(buf.AsSpan(iCharset,nCharset));
                     UTF = Encoding.GetEncoding(CharS);
                  } catch { }
                }
                if(R==2) {
                  await send_cgi().ConfigureAwait(false);
                } else {
                  await send_prg().ConfigureAwait(false);
                }
              }
            } else {
              if(!gzExists(true)) {
                if(CheckFile()) {
                  putHead(true);
                } else {
                  putHead(false);
                  nres = 0;
                  if(F.DocumentRoot.Length + F.DirectoryIndex.Length <= res.Length) {
                    F.DocumentRoot.AsSpan().CopyTo(res.AsSpan(nres));
                    nres += F.DocumentRoot.Length;
                    F.DirectoryIndex.AsSpan().CopyTo(res.AsSpan(nres));
                    nres += F.DirectoryIndex.Length;
                  }
                  if(!gzExists(false)) {
                    if(!CheckFile()) {
                      R = 0;
                      if(nres+1 < res.Length) {
                        res[nres++]= ' ';
                        res[nres++]= '-';
                      }
                      await failure("404 Not Found"u8).ConfigureAwait(false);
                    }
                  }
                }
              }
              if(R==1) await typeAsync().ConfigureAwait(false);
            }
          } else {
            if(nres > 0) {
              if(nres+2 < res.Length) {
                res[nres++]= ' ';
                res[nres++]= '-';
                res[nres++]= '-';
              }
              await failure("403 Forbidden"u8).ConfigureAwait(false);

              // На первый раз пропускаем, но счетчик у этого IP увеличиваем
              if(F.ifIP(IP)) Interlocked.Increment(ref F.iIP);
            }
          }

          // Правильное закрытие потока
          try {
            await stream.FlushAsync().ConfigureAwait(false);
            if(Prot) await ((SslStream)stream).ShutdownAsync().ConfigureAwait(false);
          } finally {
            stream?.Close();
          }

          client.Close();
          if(R > 1) {
            if(R > 2) {
              F.clear_prg(m);
            } else {
              F.clear_cgi(m);
            }
          }

          if(F.log9 > 0) {
            n = DateTime.UtcNow.Subtract(dt1).TotalMilliseconds;
            char[] rentBuffer = ArrayPool<char>.Shared.Rent(512);
            string n_fmt = n > 9999 ? "****" : n.ToString("0000");
            string m_fmt = R > 1 ? $"/{m}" : "  ";
            char cProt = Prot ? '/' : '|';
            if(IP.AddressFamily == AddressFamily.InterNetwork ?
                rentBuffer.AsSpan().TryWrite(
                  $"{cProt}{n_fmt} {IP,-15}{j_fmt}{m_fmt}\t{res.AsSpan(0, nres)}", out i):
                rentBuffer.AsSpan().TryWrite(
                  $"{cProt}{n_fmt} {IP}{j_fmt}{m_fmt}\t{res.AsSpan(0, nres)}", out i))
            {
              F.log2(rentBuffer.AsMemory(0, i));
            } else {
              ArrayPool<char>.Shared.Return(rentBuffer);
            }
          }
          Init();
        } else {
          client.Close();
        }
      }
    }

    // Читаем заголовки из потока
    async Task getHeadersAsync(Stream stream) {
      while (l) {
        await sReadAsync(stream).ConfigureAwait(false);
        if(i > 0) {
           totalBytesRead += i;
           getHeader(buf.AsSpan(0, totalBytesRead));
           if(l && totalBytesRead > F.MaxHeaderSize) {
             l = false;
             R = 0;                 // Вероятно это атака
           }
        } else {
          l = false;
        }
      }
    }

    void getHeader(Span<byte> bufSpan) {
      ReadOnlySpan<byte> remainder;
      ReadOnlySpan<byte> currentLine;
      ReadOnlySpan<byte> reso = ReadOnlySpan<byte>.Empty;
      ReadOnlySpan<byte> host = ReadOnlySpan<byte>.Empty;
      ReadOnlySpan<byte> field = ReadOnlySpan<byte>.Empty;

      // Внутренний цикл: разбираем только полностью пришедшие строки
      while (true) {
         remainder = bufSpan.Slice(parsedPosition);
         k = remainder.IndexOf(b10);
         if (k < 0) break; 
         currentLine = remainder.Slice(0, k).Trim(b10);
// F.log("|" + F.UTF8.GetString(currentLine)+"|");

         if (firstLine == 0) {
             firstLine = k + 1;
             i = currentLine.IndexOf((byte)' ');
             if(i > 0) {
                field = currentLine.Slice(0, i++);
                if(field.SequenceEqual("GET"u8) || 
                   field.SequenceEqual("POST"u8)|| 
                   field.SequenceEqual("PUT"u8)) {
                   if(!field.SequenceEqual("GET"u8)) {
                      Content_Length= -1;              // Максимальный POST без
                   }                                   // заголовка Content-Length

                   // Переводим метод в строку
                   // Method = F.UTF.GetString(field);
                   // Отрезаем метод и ищем ресурс (все, что после пробела)
                   remainder = currentLine.Slice(i).TrimStart(TrailingGarbage1);
                   i1 = parsedPosition + i;            // Позиция reso относительно buf
            
                   // Ищем второй пробел (разделитель между URI ресурса и HTTP/1.1)
                   i = remainder.IndexOf((byte)' ');
                   if(i > 0) reso = remainder.Slice(0, i);
                }
             }
         } else {

            if(currentLine.Length > 3) {
              i = currentLine.IndexOf((byte)':');
              if(i > 0) {
                field  = currentLine.Slice(0, i++);
                remainder = currentLine.Slice(i);

                // Проверяем, заголовок ли это HOST
                if (needHost && EqualsOrdinalIgnoreCase(field, "host"u8)) {
                  // Сохраняем значение Host, если нужно
                  // this.Host = F.UTF.GetString(remainder);
                  host = remainder.TrimStart(TrailingGarbage3);
                  needHost = false;
                } else if (needContentType &&
                           EqualsOrdinalIgnoreCase(field, "content-type"u8)) {
                  k1 = remainder.IndexOf("charset="u8);
                  if(k1 != -1) {
                     k1 += 8;
                     field = remainder.Slice(k1);
                     remainder = field.TrimStart(TrailingGarbage2).TrimEnd(TrailingGarbage);
                     iCharset = field.IndexOf(remainder) + parsedPosition + k1 + i;
                     nCharset = remainder.Length;
                  }
                  needContentType = false;
                } else if (Content_Length < 0 &&
                           EqualsOrdinalIgnoreCase(field, CL)) {
                  Content_Length = ExtractContentLength(remainder);
                } else if (needContentDisposition &&
                           EqualsOrdinalIgnoreCase(field, "content-disposition"u8)) {
                  ifilename = parsedPosition + i;      // Позиция относительно buf
                  needContentDisposition = false;
                  ExtractFilename(bufSpan.Slice(ifilename, remainder.Length));
                }
              }
            } else {                    // Вероятно это конец заголовкам
              headersCount = 64;        // или сбой
            }

            // Считаем длину до 64 заголовков
            if(headersCount < 64) {
              headersLen += k + 1;
              headersCount++;
            }
         }

         // Двигаем курсор вперед за символ '\n'
         parsedPosition += k + 1;

         // Проверяем на пустую строку (конец блока заголовков)
         if(currentLine.Length == 0 ||
           (currentLine.Length <= 2 && currentLine[currentLine.Length-1] == 13)) {
             l= false;
             break;
         }

         // ТОЧКА ПЕРЕГИБА: Если стартовая строка и HOST найдены, вызываем PrepResource()
         if(needLastLine && firstLine>0 && !needHost) {
           PrepResource(bufSpan, reso, host);
           needLastLine = false;       // Блокируем повторный вызов
           switch (R) {
           case 0:
           case 1:
             l = false;            // Дальше читать бессмысленно
             break;
           case 2:
             m = -1;
             if(F.cgia && F.freeCGI.TryPop(out m)) {
                if(F.cgib[m]==0) {
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
           case 3:
             m = -1;
             if(F.vfpa != null && F.freeVFP.TryPop(out m)) {
                if(F.vfpb[m] == 0) {
                  ts = Task.Run(() => F.start_VFP(m));
                }
             }
             break;
           }
         }
      }
    }

    void PrepResource(Span<byte> bufSpan, ReadOnlySpan<byte> reso,
                      ReadOnlySpan<byte> host) {
      if(reso.IsEmpty) {
        R= 0;
      } else {
        ReadOnlySpan<char> extSpan= ".";

        // 1. Получаем смещение и длину reso относительно buf и декадируем на месте
        // на текущий момент это уже есть i1!!
        //i1 = (int)Unsafe.ByteOffset(ref MemoryMarshal.GetArrayDataReference(buf),
        //                            ref MemoryMarshal.GetReference(reso));

        k= UrlDecode(bufSpan.Slice(i1, reso.Length), out i7); // Длина раскодированного reso
        if(i7>0) {
          iQUERY_STRING = i1 + i7 + 1;
          nQUERY_STRING = k - i7 - 1;
        }

        ReadOnlySpan<byte> resSpan = bufSpan.Slice(i1, i7 > 0 ? i7 : k);
        ReadOnlySpan<byte> subSpan = Before1(host, (byte)':');
        i= 0;
        nres = F.DocumentRoot.Length;
        F.DocumentRoot.AsSpan().CopyTo(res.AsSpan(0));
        try {
// F.log(" 1. i1=" +i1+" |"+F.UTF8.GetString(resSpan));
// F.log(" 1. i1=" +i1+" |"+F.UTF8.GetString(buf,i1,reso.Length));
// F.log(" 1. res=" + new string(res)+"|");
// F.log(" 1. reso=" + F.UTF8.GetString(reso)+"|");
// F.log("  subSpan=|"+F.UTF8.GetString(subSpan)+"|");
// F.log("  resSpan=|"+F.UTF8.GetString(resSpan)+"|");
          if(subSpan.Length > 0) i += F.UTF8.GetChars(subSpan, res.AsSpan(nres));
          if(resSpan.Length > 0) i += F.UTF8.GetChars(resSpan, res.AsSpan(nres+i));
          nres += i;
        } catch (ArgumentException) {
          // Сработает ТОЛЬКО если буфер res оказался меньше, чем пришедший URL
          // Здесь можно отдать ошибку 414 URL Too Long
          char[] rentBuffer = ArrayPool<char>.Shared.Rent(32);
          if(rentBuffer.AsSpan().TryWrite($"\tError: URL buffer overflow!",
                        out i)) {
            F.log2(rentBuffer.AsMemory(0, i));
          } else {
            ArrayPool<char>.Shared.Return(rentBuffer);
          }
        }

        // ".." в запроах недопустимы в целях безопасности (i без nres!)
        if(res.AsSpan(F.DocumentRoot.Length,i).IndexOf("..") < 0) {

          if(res[nres-1] == '/') {
            if(nres + F.DirectoryIndex.Length <= res.Length) {
              F.DirectoryIndex.AsSpan().CopyTo(res.AsSpan(nres));
              nres += F.DirectoryIndex.Length;
            }
          }
          extSpan = Path.GetExtension(res.AsSpan(0, nres));
          if(extSpan.Length > 0) {
            extSpan = extSpan[1..];   // Убрать первую точку
          } else {
            if(CheckFile(".", F.Ext)) {
              extSpan = F.Ext;
              i= F.Ext.Length + 1;
              if(nres+i <= res.Length) {
                res[nres] = '.';
                F.Ext.AsSpan().CopyTo(res.AsSpan(nres+1));
                nres += i;
              }
            } else if(CheckFile(".prg")) {
              extSpan = "prg";
              if(nres + 4 <= res.Length) {
                ".prg".AsSpan().CopyTo(res.AsSpan(nres));
                nres += 4;
              }
            } else if (CheckFile(dir16: 1)) {
              i= F.DirectoryIndex.Length + 1;
              if(nres+i <= res.Length) {
                res[nres] = '/';
                F.DirectoryIndex.AsSpan().CopyTo(res.AsSpan(nres + 1));
                nres += i;
              }
              extSpan = Path.GetExtension(F.DirectoryIndex.AsSpan());
              if(extSpan.Length > 0) extSpan = extSpan[1..];
            } else if(!CheckFile()) {
              extSpan = "html";
              if(nres + 5 <= res.Length) {
                ".html".AsSpan().CopyTo(res.AsSpan(nres));
                nres += 5;
              }
            }
          }
        }

        R = 1;
        switch (extSpan) {
        case "html": 
          Content_T = 0;
          break;
        case "svg":  
          Content_T = 1; 
          break;
        case "gif":  
          Content_T = 2; 
          break;
        case "png":  
          Content_T = 3; 
          break;
        case "jpeg":
        case "jpg":  
          Content_T = 4; 
          break;
        case "js":   
          Content_T = 5; 
          break;
        case "css":  
          Content_T = 6; 
          break;
        case "ico":  
          Content_T = 7; 
          break;
        case "mp4":  
          Content_T = 8; 
          break;
        case "txt":
        case "":
          Content_T = -1;
          isCC = false;
          break;
        default:
          isCC = false;
          if(extSpan.SequenceEqual(F.Ext)) {
            R = 2;
          } else if(extSpan.SequenceEqual("prg")) {
            R = 3;
          } else {
            R = 0; // Недопустимое расширение
          }
          break;
        }
      }
    }

    // Ищет маркер с начала байтовой строки и возвращает начальную часть
    static ReadOnlySpan<byte> Before1(ReadOnlySpan<byte> x, byte marker) {
      int k = x.IndexOf(marker);
      if(k < 0) return x.TrimEnd(TrailingGarbage3);
      return x.Slice(0, k);
    }

    // Проверка существования файла без аллокаций строк
    bool CheckFile(ReadOnlySpan<char> Part2 = default,
                   ReadOnlySpan<char> Part3 = default, int dir16 = 0) {
      int total = nres + Part2.Length + Part3.Length;

      if(total > res.Length) return false;

      int current = nres;

      if(Part2.Length > 0) {
        Part2.CopyTo(res.AsSpan(current));
        current += Part2.Length;
      }

      if(Part3.Length > 0) {
        Part3.CopyTo(res.AsSpan(current));
      }

      // 1. Извлекаем из массива строго собранную строку пути нужной длины
      string finalPathToCheck = res.AsSpan(0, total).ToString();

      // 2. Вызываем стандартные методы .NET, передавая им строку
      // dir16 == 0 -> ищем файл, dir16 == 1 (или любое другое) -> ищем папку
      return dir16 == 0 ? File.Exists(finalPathToCheck) :
                          Directory.Exists(finalPathToCheck);
    }

    // Полный путь ресурса
    static string fullres(ReadOnlySpan<char> x) {
      if (x.IsEmpty) return string.Empty;
      return Path.GetFullPath(x.ToString()).Replace('\\', '/');
    }

    // Высокопроизводительное сравнение ASCII-байт без учета регистра
    static bool EqualsOrdinalIgnoreCase(ReadOnlySpan<byte> span,
                                        ReadOnlySpan<byte> value) {
      // Если длины не совпадают, строки точно разные (быстрый выход)
      if (span.Length != value.Length) return false;

      for (int i = 0; i < span.Length; i++) {
        byte b1 = span[i];
        byte b2 = value[i];

        // Если байты не равны, проверяем, не являются ли они одной ASCII-буквой в разном регистре
        if(b1 != b2) {
          // Переводим обе буквы в нижний регистр (работает только для ASCII-символов заголовков HTTP)
          if (b1 >= 65 && b1 <= 90) b1 += 32;
          if (b2 >= 65 && b2 <= 90) b2 += 32;
          if (b1 != b2) return false;
        }
      }
      return true;
    }

    // Безопасное выделение параметра длины в заголовке Content-Length
    static long ExtractContentLength(ReadOnlySpan<byte> value) {
       return Utf8Parser.TryParse(value.TrimStart(TrailingGarbage1),
                                  out long cl, out _) ? cl : -1;
    }

    // Выделение параметра failname
    void ExtractFilename(Span<byte> value) {
        int startIdx = -1;
        int skipLength = 0;

        // 1. Ищем один из четырех шаблонов
        if ((startIdx = value.IndexOf("filename*=UTF-8''"u8)) != -1) skipLength= 17;
        else if ((startIdx = value.IndexOf("filename*=utf-8''"u8)) != -1) skipLength= 17;
        else if ((startIdx = value.IndexOf("filename=\""u8)) != -1) skipLength = 10;
        else if ((startIdx = value.IndexOf("filename="u8)) != -1) skipLength = 9;

        if (startIdx == -1) return;

        // Вычисляем начало самого значения имени файла относительно value
        int localValueStart = startIdx + skipLength;

        // Считаем длину имени файла
        nfilename = value.Slice(localValueStart).TrimEnd(TrailingGarbage).Length;

        ifilename += localValueStart;                               // Позиция относительно buf
        ifilename2 = ifilename + nfilename;                         // Позиция за именем файла
        nfilename = UrlDecode(value.Slice(localValueStart, nfilename), out _);
    }

    // Специальный метод для извлечения filename из строки
    string ExtractFilenameAsString(ReadOnlySpan<char> value) {
      int startIdx = -1;
      int skipLength = 0;

      if((startIdx = value.IndexOf("filename*=UTF-8''")) != -1) skipLength = 17;
      else if ((startIdx = value.IndexOf("filename*=utf-8''")) != -1) skipLength = 17;
      else if ((startIdx = value.IndexOf("filename=\"")) != -1) skipLength = 10;
      else if ((startIdx = value.IndexOf("filename=")) != -1) skipLength = 9;

      if(startIdx == -1) return string.Empty;

      int localValueStart = startIdx + skipLength;

      // Срез чистого имени файла (еще внутри строки FoxPro)
      ReadOnlySpan<char> filenamePart = value.Slice(localValueStart).TrimEnd(TrailingGarbageChars);

      // 2. Если процентов нет (обычная строка из FoxPro)
      if(filenamePart.IndexOf('%') == -1) return filenamePart.ToString();

      // 3. Если проценты всё же есть (для общего стиля), декодируем без создания промежуточных строк
      // System.Net.WebUtility умеет декодировать Span прямо в буфер, начиная с .NET Core 3.0+
      // (Этот метод возвращает количество записанных символов)
      return WebUtility.UrlDecode(filenamePart.ToString());
    }

    // Закодировать как валидный URL
    /*string EncodeFileName(string originalFileName) {
      // Этот метод переведет "документ.pdf" в "%d0%b4%d0%be%d0%ba%d1%83%d0%bc%d0%b5%d0%bd%d1%82.pdf"
      // Важно: он кодирует спецсимволы и пробелы (в %20), но оставляет расширение файла нормальным
      string encoded = HttpUtility.UrlEncode(originalFileName, F.UTF8);
    
      // HTTP-спецификация для Content-Disposition требует, чтобы пробелы были именно %20, а не '+'
      encoded = encoded.Replace("+", "%20"); 
    
      return encoded;
    }*/

    // Перекодирование URL на участке buf
    static int UrlDecode(Span<byte> slice, out int i7) {
      byte current, decodedByte, h1, h2;
      int endIdx = slice.Length;
      int writeIdx = 0;
      int readIdx = 0;
      i7 = -1;

      while (readIdx < endIdx) {
        current = slice[readIdx];

        // 1. Декодируем %XX
        if(current == (byte)'%' && readIdx + 2 < endIdx) {
          h1 = slice[readIdx + 1];
          h2 = slice[readIdx + 2];
          decodedByte = (byte)((HexToInt(h1) << 4) | HexToInt(h2));
          readIdx += 3;
        } else {
          decodedByte = current;
          readIdx++;
        }
        slice[writeIdx] = decodedByte;
        if(i7 < 0 && decodedByte == (byte)'?') i7 = writeIdx;
        writeIdx++;
      }

      // Возвращаем новую (сжатую) длину данных
      return writeIdx;
    }
    // Быстрая побитовая конвертация HEX-символа в число
    static int HexToInt(byte ch) {
      return ch switch {
        >= (byte)'0' and <= (byte)'9' => ch - '0',
        >= (byte)'A' and <= (byte)'F' => ch - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => ch - 'a' + 10,
        _ => 0
      };
    }

    void putHead(bool CT) {
      // CT - true, тип контента не изменяется
      //      false, тип контента стал html.
      isDate = true;  // head="Date: "+dt1.ToString("R")+"\r\n"+isCC
      isCT = CT;      // + (CT? Content_T : F.CT+": text/html\r\n")
    }

    bool gzExists(bool CT) {
      bool l = CheckFile(".gz");
      if(l) {
        putHead(CT);
        isGzip = true;     // Маркер того, что файл сжат
        ".gz".AsSpan().CopyTo(res.AsSpan(nres));
        nres += 3;
      }
      return l;
    }

    ValueTask failure(ReadOnlySpan<byte> xSpan) {
      k= 0;
      SetToBuf(H1);
      SetToBuf(xSpan);
      SetToBuf(CRLF);
      return stream.WriteAsync(buf.AsMemory(0, k));
    }

    // Вывод текстового сообщения длиной до 1 буфера
    ValueTask send_txtAsync(ReadOnlySpan<byte> xSpan) {
      SetToBuf(xSpan);
      return stream.WriteAsync(buf.AsMemory(0, k));
    }

    // Асинхронное Чтение данных
    async Task sReadAsync(Stream stream) {

      // Гарантированно оживляем источник токенов
      if (readCts == null || !readCts.TryReset()) {
         readCts?.Dispose();
         readCts = new CancellationTokenSource();
      }
      readCts.CancelAfter(F.tw);

      try {
        i = await stream.ReadAsync(buf.AsMemory(totalBytesRead,
                  nbuf), readCts.Token);

        // Дополнительная проверка на конец потока (EOF)
        if(i > 0) {
          if(eof == 0) {
            dt1 = DateTime.UtcNow;
            nbuf = F.HeaderBufSize2;  // Увеличим буфер
            eof = 1;                  // Обычное чтение
          }
        } else {
          eof = 3;        // Конец потока
          l = false;
        }
      } catch(OperationCanceledException) {

        // Сюда мы гарантированно прилетим при таймауте F.tw, 
        // при этом фоновая задача в ОС гарантированно УНИЧТОЖИТСЯ
        i=eof= -1;       // Таймаут приравнивается сетевой ошибке

      } catch(Exception) {
        i=eof= -1;       // Сетевая ошибка
        l = false;
      }
    }

    // Отправка файла
    async Task typeAsync() {
      string path = res.AsSpan(0, nres).ToString(); 
      await using var fs = OpenFileReadAsync(path);
    
      k = 0;
      SetToBuf(OK);
      SetHead();
      SetToBuf(GetCT(Content_T));
      SetToBuf(CL);
      SetToBuf(": "u8);
      SetToBuf(fs.Length);
      SetToBuf(CRLF);
      SetToBuf(CRLF);

      // Пишем заголовки и сразу за ними копируем файл
      await stream.WriteAsync(buf.AsMemory(0, k)).ConfigureAwait(false);
      await fs.CopyToAsync(stream).ConfigureAwait(false);
    }

    int HeadCount() {
      int count = 37;  // Длина обязательного заголовка Date
      if(isGzip) count += Gzip.Length;
      if(isCC)   count += CC.Length;
      return count;
    }

    void SetHead() {
      SetToBuf("Date: "u8);
      SetToBuf(F.CachedDateBytes);
      SetToBuf(CRLF);
      if(isCC) SetToBuf(CC);
      if(isGzip) SetToBuf(Gzip);
    }

    // Занести в буфер байтовую строку
    void SetToBuf(ReadOnlySpan<byte> xSpan) {
      if(k + xSpan.Length > buf.Length) {
        // Если не влезает, копируем только то, что поместится до конца буфера
        int remain = buf.Length - k;
        if(remain > 0) {
          xSpan.Slice(0, remain).CopyTo(buf.AsSpan(k));
          k = buf.Length;
        }
      } else {
        // Если всё помещается
        xSpan.CopyTo(buf.AsSpan(k));
        k += xSpan.Length;
      }
    }

    // Занести в буфер существующую строку
    void SetToBuf(ReadOnlySpan<char> x) {
      Span<byte> xSpan = buf.AsSpan(k);
      F.UTF8.GetEncoder().Convert(x, xSpan, flush: true, out int _, out int iAdd, out bool _);
      k += iAdd;
    }

    // Занести в буфер число int или long
    void SetToBuf(long value) {
      if(value.TryFormat(buf.AsSpan(k), out int iAdd)) k += iAdd;
    }

    // Запись хвоста от заголовков
    // Открыть файл, если он не открыт
    async Task OpenFileAsync(string filename) {
      await Task.Yield();
      if(File.Exists(filename)) {
        File.Delete(filename);
      } else {
        var dirInfo = new DirectoryInfo(new string(dirname, 0, ndirname));
        if(!dirInfo.Exists) dirInfo.Create();
      }

      // Настройки для экстремальной производительности в .NET 10
      var options = new FileStreamOptions {
        Mode = FileMode.Create,
        Access = FileAccess.Write,
        Share = FileShare.None,
        BufferSize = 65536, 
        Options = FileOptions.Asynchronous
      };

      // Если файл большой и размер известен заранее, резервируем место на диске
      // одной операцией. Это исключает фрагментацию файла и многократные тяжелые
      // запросы к ОС
      if(Content_Length > 1048576) options.PreallocationSize = Content_Length;

      file1 = new FileStream(filename, options);
      try {  // Пишем хвост после заголовков
        if(Actual_Length > 0) {
          await file1.WriteAsync(System.MemoryExtensions.AsMemory(
                buf, parsedPosition, (int)Actual_Length)).ConfigureAwait(false);
        }
      } catch { }
    }

    async Task POSTAsync(){
      // Размер хвоста POST в буфере вычисляется:
      Actual_Length = totalBytesRead - parsedPosition;
      if(Content_Length< 0) Content_Length= 100000000;
      string filename= string.Empty;

      // Определяем и проверяем наличие имени файла для данных
      if(nfilename>0 || Content_Length>(R==2? F.post:F.bu)) {
        if(dirname.AsSpan().TryWrite($"{F.DirectorySessions}/{IP}_{point.Port}", out ndirname)){
          Span<char> pathBuffer = stackalloc char[512];
          i = 0;
          dirname.AsSpan(0, ndirname).CopyTo(pathBuffer);
          i += dirname.Length;
          pathBuffer[i++] = '/';
          if(nfilename == 0) {
            DateTime.Now.TryFormat(pathBuffer.Slice(i), out i1, "HHmmssfff");
            i += i1;
          } else {
            ReadOnlySpan<byte> nameBytes = buf.AsSpan(ifilename, nfilename);
            i1 = UTF.GetChars(nameBytes, pathBuffer.Slice(i));
            i += i1;
          }
          filename = new string(pathBuffer.Slice(0, i));
          nfilename= filename.Length;

          // Открываем файл и записываем хвост
          Task fileOpenTask = OpenFileAsync(filename);

          // Если в потоке файл
          if(R==2) {
            await cgi_start(filename).ConfigureAwait(false);  // Записываем заголовки

          } else {
            prg_start(filename);        // Записываем заголовки
          }

          await fileOpenTask.ConfigureAwait(false);
          await send_file().ConfigureAwait(false);
        }
      } else {

        // и если просто поток
        if(R==2) {
          await cgi_start(filename).ConfigureAwait(false);  // Записываем заголовки
          if(Actual_Length > 0) {                           // Записываем хвост
              await stdInput.WriteAsync(System.MemoryExtensions.AsMemory(
                    buf, parsedPosition, (int)Actual_Length)).ConfigureAwait(false);
          }
        } else {

          // Записываем заголовки
          prg_start(filename);

          // Записываем хвост
          if(Actual_Length > 0) {
            F.vfp[m].SetVar("STD_INPUT",buf.AsSpan(parsedPosition,
                            (int)Actual_Length).ToArray());
          }

        }
        await send_stream(R).ConfigureAwait(false);     // Записываем POST
      }
    }

    // Передаем блок заголовков
    async Task cgi_start(string filename){
      string fullPath = fullres(res.AsSpan(0, nres));
      Span<char> lengthBuffer = stackalloc char[15];     // Максимум для IPv4
      IP.TryFormat(lengthBuffer, out i);

      // --- ЧАСТЬ 1: ПОДСЧЕТ ДЛИНЫ ---
      i1 = headersLen + nQUERY_STRING + i + 75 +
           stdEnc.GetByteCount(fullPath)*2 + (isHttps? 5 : 4);
      if(nfilename > 0) i1 += nfilename + stdEnc.GetByteCount(F.Folder);

      byte[] tmpArray = ArrayPool<byte>.Shared.Rent(4096);
      try {

        // СОЗДАЕМ ФАЙЛ ДЛЯ ОТЛАДКИ (Для работы сервера замените на stdInput.BaseStream)
        //using var outputTarget = File.Create("cgi_dump.bin"); 
        //var outputTarget = stdInput.BaseStream;

        // --- ЧАСТЬ 2: ОТПРАВКА ДЛИНЫ ПАКЕТА ---
        // Пишем число i1 в начало tmpArray.
        if (Utf8Formatter.TryFormat(i1, tmpArray.AsSpan(0), out k)) {
            tmpArray[k] = 10;  // Добавляем '\n' после числа
            k++;
        }

        // --- ЧАСТЬ 3: ОТПРАВКА RES ---
        WriteStringToTmp(fullPath);
        tmpArray[k] = 10;      // Добавляем '\n' после res
        k++;
        await stdInput.WriteAsync(tmpArray.AsMemory(0,k)).ConfigureAwait(false);
//File.WriteAllBytes("dump.txt", tmpArray.AsSpan(0, k).ToArray());


        // --- ЧАСТЬ 4: ЗАГОЛОВКИ ---
        await stdInput.WriteAsync(buf.AsMemory(firstLine, headersLen)).ConfigureAwait(false);
//File.AppendAllBytes("dump.txt", buf.AsMemory(firstLine, headersLen).ToArray());

        // --- ЧАСТЬ 5: СТАТИЧЕСКИЕ ЗАГОЛОВКИ (ПИШЕМ БЕЗ ОШИБОК И БЕЗ ВЫДЕЛЕНИЯ ПАМЯТИ) ---
        // Используем tmpArray с самого начала (offset = 0)
        k = 0;
        WriteToTmp("SCRIPT_FILENAME:"u8);
        WriteStringToTmp(fullPath);
        WriteToTmp(isHttps ? "\nSERVER_PROTOCOL:https"u8
                           : "\nSERVER_PROTOCOL:http"u8);
        WriteToTmp("\nPOST_FILENAME:"u8);
        if(nfilename > 0) {
          WriteStringToTmp(F.Folder);
          WriteStringToTmp(filename);
        }
        WriteToTmp("\nQUERY_STRING:"u8);
        buf.AsSpan(iQUERY_STRING, nQUERY_STRING).CopyTo(tmpArray.AsSpan(k)); 
        k += nQUERY_STRING;
        WriteToTmp("\nREMOTE_ADDR:"u8);
        if(IP.TryFormat(tmpArray.AsSpan(k), out i)) k += i;
      } finally {
        // Обязательно возвращаем массив в пул
        ArrayPool<byte>.Shared.Return(tmpArray);
      }
      await stdInput.WriteAsync(tmpArray.AsMemory(0, k)).ConfigureAwait(false);
//File.AppendAllBytes("dump.txt", tmpArray.AsMemory(0, k).ToArray());

      //await stdInput.FlushAsync().ConfigureAwait(false);

      // Полезный макрос/лямбда для лаконичности (не делает аллокаций в куче)
      void WriteToTmp(ReadOnlySpan<byte> source) {
        source.CopyTo(tmpArray.AsSpan(k)); k += source.Length;
      }
      void WriteStringToTmp(string SystemString) {
        k += stdEnc.GetBytes(SystemString.AsSpan(), tmpArray.AsSpan(k));
      }
    }

    void prg_start(string filename) {
      var VFP = F.vfp[m];
      Span<byte> tempBytes = stackalloc byte[15];
      if(IP.TryFormat(tempBytes, out i)) {
        byte[] VFPbytes = new byte[i];
        tempBytes.Slice(0,i).CopyTo(VFPbytes);
        VFP.SetVar("REMOTE_ADDR", VFPbytes);
      }
      VFP.SetVar("SERVER_PROTOCOL", (isHttps? "https": "http"));
      VFP.SetVar("SCRIPT_FILENAME",fullres(res.AsSpan(0, nres)));
      VFP.SetVar("HTTP_HEADERS",buf.AsSpan(firstLine, headersLen).ToArray());
      VFP.SetVar("QUERY_STRING",buf.AsSpan(iQUERY_STRING, nQUERY_STRING).ToArray());
      VFP.SetVar("POST_FILENAME", nfilename > 0? $"{F.Folder}{filename}" : string.Empty);
    }

    // Передача данных POST из потока в объект
    async Task send_stream(byte b) {
      // Если это число больше нуля, значит, часть POST уже сидит в буфере.
      // Получить хвост моментально без копирования:
      // buf.AsSpan(parsedPosition, postTailLength);
      // Именно этот срез можно без проблем отправить в stdInput.BaseStream.WriteAsync()
      // или записать в файл.

      if(Actual_Length<Content_Length && eof==1) {
        l1 = true;
        nbuf = F.bu;    // Читаем на всю длину буфера
        while (l1) {

          // Читаем середину потока
          await sReadAsync(stream).ConfigureAwait(false);

          if(i>0) {
            Actual_Length += i;
            switch(b) {
            case 2:
              await F.proc[m].StandardInput.BaseStream.WriteAsync(
                          buf.AsMemory(0,i)).ConfigureAwait(false);
              break;
            case 3:
              F.vfp[m].STD_INPUTADD(buf.AsSpan(0,i).ToArray());
              break;
            default:
              await file1.WriteAsync(buf.AsMemory(0,i)).ConfigureAwait(false);
              break;
            }
            l1 = Actual_Length<Content_Length;
          } else {
            l1 = false;
          }
        }
      }
    }

    // Чтение файла из трафика
    async Task send_file() {
      await send_stream(0).ConfigureAwait(false);

      // ЗАЩИТА: Если клиент прислал МЕНЬШЕ, чем обещал в Content_Length
      if(file1.Length > Actual_Length) file1.SetLength(Actual_Length);
      await file1.DisposeAsync().ConfigureAwait(false);
    }

    // Запись файла в трафик
    async Task FileToStreamAsync(string path) {
      try {
        await using var fs = OpenFileReadAsync(path);
        await fs.CopyToAsync(stream).ConfigureAwait(false);
      } catch {
        if(R > 2) { set_errVFP(); } else { set_errPython(); }
        SetToBuf("File "u8);
        SetToBuf(buf.AsSpan(ifilename, nfilename));
        await send_txtAsync(" not found."u8).ConfigureAwait(false);
      }
    }

    // Открыть файл для скоростного чтения
    static FileStream OpenFileReadAsync(string path) {
    return new FileStream(path, new FileStreamOptions {
               Mode = FileMode.Open,
               Access = FileAccess.Read,
               Share = FileShare.Read,
               Options = FileOptions.Asynchronous
           });
    }

    void set_errPython() {
      k= 0;
      SetToBuf(CRLF);
      SetToBuf(CRLF);
      SetToBuf("Error in Python: "u8);
    }
    void set_errVFP() {
      k= 0;
      SetToBuf(OK);
      SetHead();
      SetToBuf(GetCT(-1));
      SetToBuf(CRLF);
      SetToBuf("Error in FoxPro: "u8);
    }

    async Task send_cgi() {
      if(m < 0) {

        // Вывести сообщение об отсутствии интерпретатора
        k= 0;
        SetToBuf("There is no \""u8);
        SetToBuf(F.Proc);
        await send_txtAsync("\" on the server :("u8).ConfigureAwait(false);
        return;
      }

      try{
        if(ts != null && await ts.ConfigureAwait(false)) m = F.db;
      } catch(Exception) {
        m = F.db;
      }
      if(m >= F.db) {

        // Вывести сообщение, что все доступные процессы интерпретатора заняты
        k= 0;
        SetToBuf("All "u8);
        SetToBuf(F.db);
        SetToBuf(" \""u8);
        SetToBuf(F.Proc);
        await send_txtAsync("\" processes are busy :("u8).ConfigureAwait(false);
        return;
      }

      stdInput = F.proc[m].StandardInput.BaseStream;
      stdEnc = F.proc[m].StandardInput.Encoding;
      await POSTAsync().ConfigureAwait(false);  // Чтение данных POST
      stdInput.Close();
      if(eof > 0) {       // Если нет разрыва связи
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
        k = 0;              // Длина подготовленного заголовка
        l1 = false;         // Пока filename не найден
        try {

          // Прочитать ответ во вторую половину buf
          i1 = await F.proc[m].StandardOutput.BaseStream.ReadAsync(buf.AsMemory(F.bu2, F.bu2),
                     exeCts.Token);

          // Проверить код возврата
          if(i1>5) {
            i=F.valInt(buf.AsSpan(F.bu2, 4));
            if(i>=100 && i<=599) {
              nres = buf.AsSpan(F.bu2, i1).IndexOf(b10);
              if(nres > 0) {
                // Обнаружена строка статуса возврата длиной nres
                i = buf.AsSpan(F.bu2, nres).IndexOf((byte)';');
                if(i == -1) {
                  i = nres+1;
                  k = k1 = F.bu2 - H1.Length - HeadCount();
                } else {
                  // Обнаружен дополнительный параметр (filename)
                  l1 = true;
                  nres -= i;
                  ExtractFilename(buf.AsSpan(F.bu2+i, nres));
                  k = k1 = F.bu2 - H1.Length - HeadCount() + nres - 1;
                }
                Span<byte> statusBytes = stackalloc byte[i];
                buf.AsSpan(F.bu2, i).CopyTo(statusBytes);
                SetToBuf(H1);
                SetToBuf(statusBytes);
                if(l1) SetToBuf(CRLF);
              }
            }
          }
          if(k==0) {
            k = k1 = F.bu2 - OK.Length - HeadCount();         // Начало ответа
            SetToBuf(OK);
          }
          SetHead();
          await stream.WriteAsync(buf.AsMemory(k1, F.bu2+i1-k1)).ConfigureAwait(false);
          if(!F.proc[m].HasExited) await F.proc[m].StandardOutput.BaseStream
                                  .CopyToAsync(stream).ConfigureAwait(false);
          if(l1) await FileToStreamAsync(F.UTF8.GetString(buf, ifilename,
                                         nfilename)).ConfigureAwait(false);
          //await stream.FlushAsync().ConfigureAwait(false);
        } catch (OperationCanceledException) {
          set_errPython();
          SetToBuf("The maximum calculation duration of "u8);
          SetToBuf(F.i8);
          await send_txtAsync(" ms has been exceeded."u8).ConfigureAwait(false);
        } catch (Exception e) {
          set_errPython();
          SetToBuf(e.Message);
          await stream.WriteAsync(buf.AsMemory(0, k)).ConfigureAwait(false);
        }
      }
    }

    async Task send_prg() {
      if(m < 0) {

        // Вывести сообщение об отсутствии VFP в реестре
        k= 0;
        await send_txtAsync("\"foxpro9.Shell\" is missing in the Windows registry :("u8)
             .ConfigureAwait(false);
        return;
      }

      if(ts != null && (bool)await ts.ConfigureAwait(false)) m = F.db;
      if(m >= F.db) {
        // Вывести сообщение, что все процессы VFP заняты
        k= 0;
        SetToBuf("All ");
        SetToBuf(F.db);
        await send_txtAsync(" FoxPro9.exe processes are busy :("u8).ConfigureAwait(false);
        return;

      } else {
        await POSTAsync().ConfigureAwait(false);     // Чтение данных POST
        if(eof < 0) return;                          // Если обнаружен разрыв связи
      }
      if (exeCts == null || !exeCts.TryReset()) {
         exeCts?.Dispose();
         exeCts = new CancellationTokenSource();
      }

      // Если выполнение prg не закончилось за 25 минут, то аварийно снять процесс
      exeCts.CancelAfter(F.i8);

      // Вывод полученных данных prg-скрипта
      k = 0;              // Длина подготовленного заголовка
      l1 = false;         // Пока filename не найден
      string ret = string.Empty;
      try{
        ret = await Task.Run(() => F.vfp[m].Eval()).WaitAsync(exeCts.Token) as string;
        if(ret != null && ret.Length > 5) {
          i = F.valInt(ret, 4);
          if(i>=100 && i<=599) {
            // Обнаружена строка статуса возврата длиной nres
            i = ret.IndexOf(';');
            if(i != -1) {
              // Обнаружен дополнительный параметр (filename)
              l1 = true;
            }
            SetToBuf(H1);
            SetToBuf(ret);
            SetToBuf(CRLF);
          }
        }
        if(k == 0) {
          SetToBuf(OK);
          SetHead();
        }

        // Отправляем заголовки
        await stream.WriteAsync(buf.AsMemory(0, k)).ConfigureAwait(false);

        // Получаем и отправляем в stream массив из FoxPro
        if(F.vfp[m].STD_OUTBIN() is System.Array comArray) {
          Actual_Length = comArray.LongLength - 1;
          if(Actual_Length > 0) {
            GCHandle handle = GCHandle.Alloc(comArray, GCHandleType.Pinned);
            try {
              IntPtr intPtr = Marshal.UnsafeAddrOfPinnedArrayElement(comArray, 1);
              unsafe {
                VFPstream = new UnmanagedMemoryStream((byte*)intPtr,
                                   Actual_Length, Actual_Length, FileAccess.Read);
              }
              using (VFPstream) {
                await VFPstream.CopyToAsync(stream).ConfigureAwait(false);
              }
            } finally {
              if(handle.IsAllocated) handle.Free();
            }
          } else {
            set_errVFP();
            SetToBuf(F.vfp[m].ERROR_MESS as string is string VFPerr ?
                     VFPerr : ReadOnlySpan<char>.Empty);
            await stream.WriteAsync(buf.AsMemory(0, k)).ConfigureAwait(false);
          }
        }
        if(l1) {
          await FileToStreamAsync(ExtractFilenameAsString(ret.AsSpan())).ConfigureAwait(false);
        }
      } catch(OperationCanceledException) {
        set_errVFP();
        SetToBuf("The maximum calculation duration of "u8);
        SetToBuf(F.i8);
        await send_txtAsync(" ms has been exceeded."u8).ConfigureAwait(false);
        F.vfpQuit(in m);
        F.vfpb[m]= 0;
      } catch(Exception e) {
        set_errVFP();
        SetToBuf(e.Message);
        await stream.WriteAsync(buf.AsMemory(0, k)).ConfigureAwait(false);
      }
    }
  }
}
