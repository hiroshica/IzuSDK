# ProjectおよびVSCodeの設定

- Project用のフォルダを作成し、以下の攻勢でサブフォルダを作成しておく。   
(少なくともこの[Wiki](https://github.com/Stephane-D/SGDK/wiki)は熟読しておくように)

```
━┿ ProjectTop
 ┣ .vscode :VSCode用フォルダ
 ┣ src     :ソース類を入れておくフォルダ
 ┣ inc     :ソース類を入れておくフォルダ
 ┗ res     :データ群を入れておくフォルダ
```

.vscodeに以下のファイルを作る。(これにより build用の様々なtaskやdebuggerの設定が施される)
```
setting.json

{
    "files.associations": {
        "joy.h": "c",
        "sprite_eng.h": "c",
        "timer.h": "c",
        "format": "c",
        "initializer_list": "c",
        "utility": "c",
        "xstring": "c",
        "xutility": "c"
    },
    "files.exclude": {
        "**/*.d": true,
        "**/*.dll": true,
        "**/*.exe": true,
        "**/*.jar": true,
        "**/*.o": true,
        "archive/**": true,
        "cb/**": true,
        "tools/archives": true
    },
    "terminal.integrated.env.windows": {
    },
    "terminal.integrated.defaultProfile.windows": "Command Prompt",
    "terminal.integrated.inheritEnv": true,
}
```
```
c_cpp_properties.json

{
    "configurations": [
        {
            "name": "SGDK",
            "browse": {
                "path": [
                    "${workspaceFolder}"
                ],
                "limitSymbolsToIncludedHeaders" : true
            },
            "includePath": [
                "${workspaceFolder}/inc/",
                "${workspaceFolder}/res/",
                "${env:SGDK}/inc/**",
                "${env:SGDK}/src/**"
            ],
        }
    ],
    "version": 4
}
```
```
launch.json

{
    "version": "0.2.0",
    "configurations": [

        {
            "name": "Debug with gdb remote",
            "request": "launch",
            "type": "cppdbg",
            "program": "${workspaceRoot}\\out\\rom.out",
            "miDebuggerServerAddress": "localhost:6868",//GensKMod debug port. Change it for use another ports
            "sourceFileMap": {
                //"${workspaceRoot}\\src": "${env:SGDK_PATH}\\src\\",
                //"C:\\Users\\hiro_\\working\\SGDK_work\\SGDK\\sample\\bench\\src": "${env:SGDK_PATH}\\src\\",
            },
            "args": [],
            "stopAtEntry": true,
            "cwd": "${workspaceFolder}",
            "environment": [],
            "externalConsole": false,
            "MIMode": "gdb",
            "launchCompleteCommand": "exec-continue",
            "miDebuggerPath": "${env:SGDK}\\bin\\gdb.exe",
            "setupCommands": [
                {
                    "text": "set directories '${workspaceFolder};$cwd;$cdir'"
                }
            ]
        }
    ]
}```
```
tasks.json

{
    "version": "2.0.0",
    "options": {
        "shell": {
            "executable": "${env:windir}\\system32\\cmd.exe",
            "args": [
                "/d",
                "/c"
            ]
        },
    },
    "type": "shell",
    
    "tasks": [
        {
            "label": "Task Sequence",
            "command": "echo ${env:SGDK}",
            "dependsOrder": "sequence",
            //"dependsOn": [
            //    "lib",
            //    "release"
            //],
            "problemMatcher": [],
            "group": {
                "kind": "build",
                "isDefault": true
            },
        },
        {
            "label": "release",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makefile.gen",
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
            "group": "build",
        },
        {
            "label": "debug",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makefile.gen",
                "debug"
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
        },
        {
            "label": "clean",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makefile.gen",
                "clean-all"
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
            "options": {
                "env": {
                    "SGDK": "C:/Users/hiro_/working/SGDK_work/SGDK",
                },
            },
        },
        {
            "label": "lib",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makelib.gen"
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
        },
        {
            "label": "libclean",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makelib.gen",
                "clean"
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
        },
        {
            "label": "libdebug",
            "command": "${env:SGDK}/bin/make",
            "args": [
                "-f",
                "${env:SGDK}/makelib.gen",
                "debug"
            ],
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared",
                "showReuseMessage": false,
                "clear": true
            },
            "problemMatcher": [],
        }
    ]
}
```