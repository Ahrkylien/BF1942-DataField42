bits 32
org 0x40BF10

%include "hook_function_common.asm"

lea     ecx, [esp-0x94]             ; part I replaced at the caller

save_regs
sub     esp, 0x300                  ; room for 128 variables 0x4 - 0x200

push    0                           ; NULL

mov     eax, [0x971eac]             ; dice::bf::setup
lea     ecx, [eax+0x7D4]            ; std::basic_string dice::bf::setup->modid
call    [0x8c30dc]                  ; c_str()
push    eax                         ; *arg6

lea     eax, [esi+0x0C]             ; map path
push    eax                         ; *arg5

lea     ecx, [edi+0x2D4]            ; std::basic_string password
call    [0x8c30dc]                  ; c_str()
mov     ecx, eax
quote_to_buf ecx, -0x200            ; *arg4 (password)

lea     ecx, [edi+0x22C]            ; std::basic_string IP:port
call    [0x8c30dc]                  ; c_str()
push    eax                         ; *arg3

quote_to_buf ergc, -0x100           ; *arg2 (register path for ergc)

push    map_identifier              ; *arg1

spawn_datafield_and_ret

map_identifier:
    db "map", 0