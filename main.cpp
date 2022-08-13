#include <stdio.h>
//#include <fstream>
#include <winreg.h>
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
//#include <ws2def.h>
#include <shellapi.h>
#include <string>
#include <iostream>
#include <sstream>
#include <algorithm>
#include "zlib.h"
#include "md5.h"

#include "masterDB.h"

using namespace std;

//https://zlib.net/
//https://github.com/madler/zlib

//http://www.zedwood.com/article/cpp-md5-function

/*
The exe gets opened with args from BF1942.exe
But it can also be opened directly without args to manage settings etc.

args:
0: identifier; "map" or "mod"
if identifier == "map" or "mod":
    1: keyRegisterPath
    2: IP:port for reconnect server
    3: password for reconnect server
    if identifier == "map": Means that the game does have the mod but not the map
        4: mapPath; "bf1942/levels/mapName/"
        5: modID
    if identifier == "mod": Means that the game doesn't have the mod
        5: modID
*/

//#define DEBUG 0

int openBF1942(const char* modID = 0, const char* ip_port = 0, const char* password = 0){
    string execPath("BF1942.exe +restart 1");
    if(modID){
        execPath.append(" +game ");
        execPath.append(modID);
    }
    if(ip_port){
        execPath.append(" +joinServer ");
        execPath.append(ip_port);
    }else
        execPath.append(" +goToInterface 6");
    if(password){
        execPath.append(" +password ");
        execPath.append(password);
    }
    return WinExec(execPath.c_str(), SW_SHOWNORMAL);
}

int getKeyHash(const string& keyRegisterPath, string& keyHash){
    char key[28];
    HKEY hKey;
    DWORD cbData = 28;
    DWORD lResult = RegOpenKeyExA(HKEY_LOCAL_MACHINE, keyRegisterPath.c_str(), 0, KEY_READ, &hKey);
    if (lResult == ERROR_SUCCESS) {
        DWORD dwRet = RegQueryValueExA(hKey, NULL, NULL, NULL, (LPBYTE) key, &cbData);
        RegCloseKey(hKey);
        if(dwRet == ERROR_SUCCESS){
//            keyHash = md5(string(key));
            return(1);
        }else{
            cout << "ERROR: Can't read key Register: " << dwRet << endl;
            return(0);
        }
    }else{
        cout << "ERROR: Can't open key Register: " << lResult << endl;
        return(0);
    }
}

int clientAwknowlage(){
    std::string word;
    while(1){
        if (word == "yes"){
            std::cin.ignore();
            return(1);
        }
        else if (word == "no"){
            std::cin.ignore();
            return(0);
        }
        std::cin >> word;
    }
}

void clientAwknowlage2(){
    cout << "Press enter to go back to the game." << endl;
    getchar();
}


int main(int argc, char** argv){
    cout << "Welcome to DataField42, the automatic download tool for BF1942" << endl << endl;
    FileDB master_db;
    master_db.init();
    if(argc > 1){
        string identifier(argv[1]);
        if((identifier == "map" && argc == 7) || (identifier == "mod" && argc == 6)){
            string keyRegisterPath(argv[2]);
            string ip_port(argv[3]);
            string password(argv[4]);
            string mapPath, modID;
            if(identifier == "map"){
                mapPath = argv[5];
                modID = argv[6];
            }else{
                modID = argv[5];
            }
            string keyHash;
            int keyHashSuccess = getKeyHash(keyRegisterPath, keyHash);
            if(keyHashSuccess){
                if(identifier == "map"){
                    if(strncmp(mapPath.c_str(), "bf1942/levels/", 14) == 0 && mapPath.back() == '/'){
                        string mapName = mapPath.substr(14, mapPath.length()-14-1);
                        string mapName_clean = mapName;
                        replace(mapName_clean.begin(), mapName_clean.end(), '_', ' ');
                        //check if map of mod is available in db
                        cout << "The server you wanted to join runs a map you don't have." << endl;
                        int handshake = master_db.handshake();
                        if(handshake == 1){
                            if(master_db.downloadMap(mapName, modID, 1)){
                                cout << "Do you want to download the map: '" << mapName_clean << "' for " << modID << "?" << endl;
                                cout << "(Type 'yes' or 'no' and hit enter)" << endl;
                                if(clientAwknowlage()){
                                    master_db.downloadMap(mapName, modID);
                                    clientAwknowlage2();
                                    return openBF1942(modID.c_str(), ip_port.c_str(), password.c_str());
                                }else{
                                    return openBF1942(modID.c_str());
                                }
                            }else{
                                cout << "We sadly can't find the file in our database." << endl;
                                cout << "Press enter to go back to the game." << endl;
                                getchar();
                                return openBF1942(modID.c_str());
                            }
                        }else if(handshake == -1){ //update
                            cout << "You need to update DataField42 to continue." << endl;
                            cout << "Do you want to update now?" << endl;
                            cout << "(Type 'yes' or 'no' and hit enter)" << endl;
                            if(clientAwknowlage()){
                                master_db.update();
                            }else{
                                return openBF1942(modID.c_str());
                            }
                        }else{//cant connect to master
                            cout << "We sadly can't connect to the central file database." << endl;
                            cout << "Press enter to go back to the game." << endl;
                            getchar();
                            return openBF1942(modID.c_str());
                        }
                    }
                }else{ //identifier == mod
                    cout << "The server you wanted to join runs a mod you don't have." << endl;
                    if(0){
                        //check if mod is available in db
                        if(1){
                            cout << "Do you want to download the mod: '" << modID << "'?" << endl;
                            cout << "(yes/no)" << endl;
                            cout << "(feature not implemented yet)" << endl;
                            cout << "Press enter to go back to the game." << endl;
                            getchar();
                            return openBF1942();
                            return openBF1942(modID.c_str(), ip_port.c_str(), password.c_str());
                        }else{
                            cout << "We sadly can't find the file in our database." << endl;
                            clientAwknowlage2();
                            return openBF1942();
                        }
                    }
                    cout << "This feature is not implemented yet" << endl;
                    clientAwknowlage2();
                    return openBF1942();
                }
            }
        }else{
            cout << "ERROR: Wrong argc for " << identifier << ": " << argc << endl;
            getchar();
        }
    }else{
        cout << "You are in the settings portal" << endl << endl;
        cout << "This feature is not implemented yet" << endl;
//        master_db.downloadMap("berlin", "bf1942");
//        cout << "___________________" << endl;
        getchar();
    }
    //Sleep(10000000000);
    return 0;
}

