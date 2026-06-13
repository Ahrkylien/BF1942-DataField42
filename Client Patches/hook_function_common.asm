ergc equ 0x8C52D0

%macro save_regs 0
    push    ebp
    push    eax
    push    ebx
    push    ecx
    push    edx
    push    edi
    push    esi
    mov     ecx, ebp                    ; old bp
    mov     ebp, esp
%endmacro

; quote_to_buf src, buf_offset
; Wraps the string at [src+i] in double quotes, stores to [ebp+buf_offset], then pushes the address.
; src may be a register (e.g. ecx) or a symbol (e.g. ergc).
%macro quote_to_buf 2
    xor     ebx, ebx
    mov     byte [ebp+%2], '"'
%%loop:
    mov     al, [%1+ebx]
    inc     ebx
    mov     [ebp+ebx+%2], al
    test    al, al
    jnz     short %%loop
    mov     byte [ebp+ebx+%2], '"'
    mov     byte [ebp+ebx+%2+1], 0
    lea     eax, [ebp+%2]
    push    eax
%endmacro

%macro spawn_datafield_and_ret 0
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
%endmacro
