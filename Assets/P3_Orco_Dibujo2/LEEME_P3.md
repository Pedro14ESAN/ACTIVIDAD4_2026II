# P3 – Orco del Dibujo 2 (Integrante 3: Dario)

Esta carpeta va dentro de `Assets/` del proyecto del grupo:
`Assets/P3_Orco_Dibujo2/` (mismo formato que `P2_Vampiro_BotonesVirtuales`).

## Qué va en cada carpeta

| Carpeta | Qué poner |
|---|---|
| 01_Dibujo_ImageTarget | `Dibujo2_Orco.png` (el dibujo hecho con IA) |
| 02_Modelo3D_Texturas | `Orco_Mixamo.fbx` descargado de Mixamo **después del auto-rig** (T-Pose, With Skin). Las 10 PNG del orco (Orc_Body_* y Orc_Accesories_*) van en `Texturas/`: el script asigna Color, Normal y AO a cada material |
| 03_Animaciones | 3 FBX de Mixamo (Without Skin) con **Idle**, **Spell** y **Jump** en el nombre, ej. `Orco_Idle_BreathingIdle.fbx` |
| 04_Animator | Lo crea el script: `AC_Orco.controller` |
| 05_VFX | Lo crea el script: `VFX_AuraOrco` (fuego verde) y `VFX_HechizoOrco` (estallido) |
| 06_Scripts | `CicloAnimacionesOrco.cs` y `Editor/ConfigurarOrcoFantasy.cs` |

## Animaciones que YA usa el vampiro (P2), no repetir

- Spell: Standing 2H Cast Spell 01
- Idle: Action Idle To Fight Idle
- Jump: Jumping Down

## Pasos en Unity

1. Copia los archivos en sus carpetas.
2. Agrega el Dibujo 2 a la base de datos de Vuforia e importa el paquete de la base.
3. En la escena: GameObject > Vuforia Engine > Image Target → elige la base y el target del Dibujo 2.
   Nómbralo `ImageTarget_Dibujo2`.
4. Selecciona ese ImageTarget → menú **FantasyAR > Configurar orco (Dibujo 2)**.
5. Play. El orco hace Idle (~4 s) → Spell (con estallido VFX) → Jump → Idle, en bucle.
   En el Editor: teclas 4 = Spell, 5 = Idle, 6 = Jump.

## Prompt para el Dibujo 2 (ChatGPT / Gemini / Copilot)

> Ilustración digital de fantasía medieval, vista frontal de cuerpo completo de un orco guerrero
> de piel verde, colmillos, armadura de cuero y metal con remaches, sosteniendo un hacha, rodeado
> de llamas verdes mágicas y runas brillantes. Fondo detallado de fortaleza de piedra con antorchas,
> estandartes, rocas y vegetación. Mucho detalle y textura en toda la imagen, colores contrastados,
> bordes nítidos, sin degradados lisos ni zonas vacías. Formato vertical 2:3, sin texto.

Al subirlo a Vuforia debe salir con **4 o 5 estrellas**. Si sale con menos, pide más detalle en el fondo.
