bits 32
org 0x40C0F0

%include "hook_function_common.asm"

mov     dword [edi+0x2B0], 0x0B     ; part I replaced at the caller

save_regs
sub     esp, 0x200                  ; room for 128 variables 0x4 - 0x200

push    0                           ; NULL

push    esi                         ; *arg5 (modid)

lea     ecx, [edi+0x2D4]            ; std::basic_string password
call    [0x8c30dc]                  ; c_str()
mov     ecx, eax
quote_to_buf ecx, -0x200            ; *arg4 (password)

lea     ecx, [edi+0x22C]            ; std::basic_string IP:port
call    [0x8c30dc]                  ; c_str()
push    eax                         ; *arg3

quote_to_buf ergc, -0x100           ; *arg2 (register path for ergc)

push    mod_identifier              ; *arg1

spawn_datafield_and_ret

mod_identifier:
    db "mod", 0
