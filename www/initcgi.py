#!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
#!!  Взаимообмен между http.net/https.net сервером и скриптами  !!
#!!  по протоколу initCGI                                       !!
#!!  Автор: А.Корниенко & AI Collaborator           02.07.2026  !!
#!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

#    Протокол быстрого CGI: initCGI (C)
# ================================================================
# 1. Перед каждым потоком, сопровождающим запрос, передается
#    блок заголовков. Первая строка в блоке содержит число байт в
#    оставшейся части блока или пустая строка, что означает
#    завершение работы сервера.
# 2. Вторая строка содержит наименование скрипта-обработчика с
#    путем.
# 3. Далее следуют строки заголовков.
# 4. Весь остальной поток POST в стандартном вводе проходит
#    скрипту-обработчику без изменений.

import os, sys, importlib.util          # в другом случае  runpy

n = int("0"+sys.stdin.readline())       # Чтение длины заголовка
if n > 1:

   # Чтение блока заголовков и формирование переменных окружения:
   lines = sys.stdin.read(n).split('\n')

   script = lines[0]

   # Безопасная разборка заголовков без риска уронить скрипт
   for line in lines[1:]:
       name, sep, value = line.partition(':')
       if sep:
          os.environ[name.strip()] = value.strip(' \r')

   # Получение и установка пути:
   os.chdir(os.path.dirname(os.path.abspath(script)))

   #runpy.run_path(script)   # Запуск скрипта обработчика запроса
   #Более быстрый вариант от AI Collaborator:
   spec = importlib.util.spec_from_file_location("cgi_script", script)
   module = importlib.util.module_from_spec(spec)
   spec.loader.exec_module(module)

   # Очистка памяти для отладки при изменении скриптов
   sys.modules.pop("cgi_script", None)

   # Ускорить отправку результата
   try:
       sys.stdout.flush()
       os.close(1)
   except Exception:
       pass
