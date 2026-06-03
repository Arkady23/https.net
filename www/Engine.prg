*!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
*!!  Взаимообмен между https.net сервером и скриптами через  !!
*!!  COM EXE повторитель                                     !!
*!!  Авторы: А.Корниенко & AI Collaborator       03.06.2026  !!
*!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

*  Протокол быстрого CGI для Visual FoxPro: vfoxpro.Engine (C)
* =============================================================
* 1. C#-сервер держит  изолированный пул процессов  initvfp.exe
*    (32-бит)
* 2. Переменные среды и POST данные передаются без дублирования
*    памяти через единый глобальный указатель env.
* 3. Метод Eval() автоматически  разделяет  выполнение  внешних
*    .prg  скриптов  с  изоляцией  путей  и  вызовы  внутренних
*    функций FoxPro.
* 4. После каждого запроса  метод clearPRG() выполняет точечный
*    сброс  ресурсов (CLOSE/CLEAR) и  контролирует  пул  памяти
*    SYS(1001).
* 5. В случае  зависания  скрипта C#-сервер делает жесткий Kill
*    по PID.

DEFINE CLASS Engine AS Custom OLEPUBLIC
  * --- Рабочие свойства CGI ---
  STD_INPUT       = ""
  STD_OUTPUT      = ""
  QUERY_STRING    = ""
  REMOTE_ADDR     = ""
  SERVER_PROTOCOL = ""
  SCRIPT_FILENAME = ""
  POST_FILENAME   = ""
  ERROR_MESS      = ""
  ERROR_CODE      = 0
  ProcessID       = 0
    
  * Храним стартовый (максимальный) объем доступной памяти пула
  PROTECTED InitialAvailablePool

  PROCEDURE Init
    =SYS(2335, 0)

    LOCAL ret

    * Устанавливаем настройки среды по умолчанию
    THIS.SetDefaultEnvironment()
          
    TRY
      =SYS(1104)  && Чистим буферы
      THIS.InitialAvailablePool = VAL(SYS(1001))
      THIS.ProcessID = _VFP.ProcessId
      ret = .T.
    CATCH TO oException
      ret = .F.
    ENDTRY
    RETURN m.ret
  ENDPROC

  PROCEDURE SetVar(VarName, Value)
    VarName = UPPER(ALLTRIM(VarName))
    THIS.AddProperty(VarName, Value)
  ENDPROC

  * Метод для выполнения любых административных команд FoxPro
  PROCEDURE DoCmd(CommandText)
    TRY
      &CommandText
    CATCH TO oException
      THIS.SetCatchError(m.oException, "DOCMD")
    ENDTRY
  ENDPROC

  * Универсальный метод вычисления выражений и запуска prg
  PROCEDURE Eval(res)
    LOCAL i, cPath, l, prg, ret
    IF TYPE("m.res") = "C"
      i = -1
    ELSE
      PUBLIC env
      env = THIS
      i = rat("/", THIS.SCRIPT_FILENAME)
      prg = subs(THIS.SCRIPT_FILENAME, m.i+1)
      res = JUSTSTEM(m.prg)+"()"
    ENDI

    TRY
      IF m.i > 0
        * Случай передачи prg (не указан параметр)
        cPath = '"'+left(THIS.SCRIPT_FILENAME, m.i)+'"'
        SET DEFA TO (m.cPath)

      ENDI
      ret = EVALUATE(m.res)
    CATCH TO oException
      IF m.i>0
        TRY
          COMPILE (m.prg)
          ret = EVALUATE(m.res)
        CATCH TO oException
          THIS.SetCatchError(m.oException, "EVAL")
        ENDTRY
      ELSE
        THIS.SetCatchError(m.oException, "EVAL")
      ENDI
    ENDTRY
    RETURN m.ret
  ENDPROC
        
  PROCEDURE clearPRG(isCustomClear)
    LOCAL i, PropCount, PropName, CurrentAvailablePool

    * --- 1. РАСШИРЕНИЕ без очистки ядра VFP ---
    * Если C#-сервер принудительно передал .T., запускаем скрипт из папки рантайма
    IF m.isCustomClear
      LOCAL res
      res = SYS(2004) + "VFPclear.prg"
      TRY
        DO (m.res)
      CATCH TO oException
        TRY
          COMPILE (m.res)
          DO (m.res)
        CATCH TO oException
          THIS.SetCatchError(m.oException, "CUSTOM CLEAR")
          THIS.DefaultClear()
        ENDTRY
      ENDTRY

    * --- 2. СИСТЕМНАЯ ОЧИСТКА ЯДРА (при отсутствии VFPclear.prg) ---
    ELSE
      THIS.DefaultClear()
    ENDIF

    * --- 3. Восстанавливаем настройки среды по умолчанию
    THIS.SetDefaultEnvironment()

    * --- 4. СИСТЕМНАЯ ОЧИСТКА ДИНАМИЧЕСКИХ СВОЙСТВ ---
    PropCount = AMEMBERS(Props, THIS, 0)
    FOR i = 1 TO m.PropCount
       PropName = UPPER(Props[m.i])
       IF PEMSTATUS(THIS, m.PropName, 4) AND ;
          NOT INLIST(m.PropName, "STD_INPUT", "STD_OUTPUT", "QUERY_STRING", "REMOTE_ADDR", ;
                    "SERVER_PROTOCOL", "SCRIPT_FILENAME", "POST_FILENAME", "ERROR_MESS", ;
                    "ERROR_CODE", "PROCESSID")
          THIS.RemoveProperty(m.PropName)
       ENDIF
    ENDFOR
    THIS.STD_INPUT       = ""
    THIS.STD_OUTPUT      = ""
    THIS.QUERY_STRING    = ""
    THIS.REMOTE_ADDR     = ""
    THIS.SERVER_PROTOCOL = ""
    THIS.SCRIPT_FILENAME = ""
    THIS.POST_FILENAME   = ""
    THIS.ERROR_MESS      = ""
    THIS.ERROR_CODE      = 0

    * --- 5. ПРОВЕРЯЕМ НА ДОПУСТИМУЮ УТЕЧКУ ПАМЯТИ ---
    =SYS(1104) 
    CurrentAvailablePool = VAL(SYS(1001))
    IF m.CurrentAvailablePool < (THIS.InitialAvailablePool * 0.5)
       RETURN .T.  
    ENDIF
    RETURN .F.
  ENDPROC

  * Внутренний метод восстановления настроек по умолчанию
  PROTECTED PROCEDURE SetDefaultEnvironment
    * Подтверждаем режим беспилотного сервера
    =SYS(2335, 0) 
    
    * Базовые безопасные системные настройки
    SET NEAR ON
    SET TALK OFF
    SET EXACT ON
    SET NOTIFY OFF
    SET CENTURY ON
    SET DELETED ON
    SET MARK TO '.'
    SET HOURS TO 24
  ENDPROC

  PROTECTED PROCEDURE DefaultClear()
    TRY
      CLEAR EVENTS        && Остановка очередей событий
      CLOSE DATABASES ALL && Безопасное закрытие таблиц и сброс буферов данных
      CLOSE ALL           && Закрытие низкоуровневых файлов и текстовых потоков
      CLEAR PROGRAM       && Очистка кэша скомпилированных prg из памяти
      CLEAR MEMORY        && Очистка локальной памяти без повреждения свойств класса
    CATCH
    ENDTRY
  ENDPROC

  * Внутренний метод централизованного логирования исключений TRY-CATCH
  PROTECTED PROCEDURE SetCatchError(oException, ContextText)
    THIS.ERROR_CODE = oException.ErrorNo
    THIS.ERROR_MESS = "VFP " + m.ContextText + " ERROR: " + oException.Message + ;
                      " (Code: " + STR(oException.ErrorNo) + ")" + ;
                      " in " + ALLTRIM(oException.Procedure) + ;
                      " Line: " + STR(oException.LineNo)
  ENDPROC
ENDDEFINE
