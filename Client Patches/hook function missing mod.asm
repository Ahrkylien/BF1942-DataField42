bits 32
org 0x40C0F0

ergc equ 0x8C52D0

patch_start:
    mov     dword [edi+0x2B0], 0x0B     ; part I replaced at the caller

    push    ebp
    push    eax
    push    ebx
    push    ecx
    push    edx
    push    edi
    push    esi
    mov     ecx, ebp                    ; old bp
    mov     ebp, esp
    sub     esp, 0x200                  ; room for 128 variables 0x4 - 0x200

    push    0                           ; NULL

    push    esi                         ; *arg5 (modid)

    lea     ecx, [edi+0x2D4]            ; std::basic_string password
    call    [0x8c30dc]                  ; c_str()
    mov     ecx, eax
    ; add quotes around password:
    xor     ebx, ebx
    mov     byte [ebp-0x200], '"'
.replace_loop_0:
    mov     al, [ecx+ebx]
    inc     ebx
    mov     [ebp+ebx-0x200], al
    test    al, al
    jnz     short .replace_loop_0
    mov     byte [ebp+ebx-0x200], '"'
    mov     byte [ebp+ebx-0x1FF], 0
    ; push local string address to stack:
    lea     eax, [ebp-0x200]
    push    eax                         ; *arg4 (password)

    lea     ecx, [edi+0x22C]            ; std::basic_string IP:port
    call    [0x8c30dc]                  ; c_str()
    push    eax                         ; *arg3

    ; add quotes around register path:
    xor     ebx, ebx
    mov     byte [ebp-0x100], '"'
.replace_loop:
    mov     al, [ergc+ebx]
    inc     ebx
    mov     [ebp+ebx-0x100], al
    test    al, al
    jnz     short .replace_loop
    mov     byte [ebp+ebx-0x100], '"'
    mov     byte [ebp+ebx-0xFF], 0
    ; push local string address to stack:
    lea     eax, [ebp-0x100]
    push    eax                         ; *arg2 (register path for ergc)

    push    mod_identifier              ; *arg1
    push    exeName                     ; *arg0
    push    exeName                     ; *cmdname
    push    2                           ; mode
    call    [0x8c34f8]                  ; _spawnl
    add     esp, 0x14                   ; 4*times of push

    mov     esp, ebp                    ; restore ESP
    pop     esi
    pop     edi
    pop     edx
    pop     ecx
    pop     ebx
    pop     eax
    pop     ebp                         ; restore caller's EBP
    ret                                 ; pop the return address into EIP

exeName:
    db "DataField42", 0x2E, "exe", 0
mod_identifier:
    db "mod", 0
