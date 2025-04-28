@echo off
@REM start cmd /k "cd ollama && run_ollama.bat"
ollama list
start cmd /k "cd server && run_server.bat" 