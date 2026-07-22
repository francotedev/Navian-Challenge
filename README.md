# NeuroPath — Planificador de trayectorias neuroquirúrgicas

**Desafío técnico XR Engineer · Navian.** Construido sobre la escena base (Unity 6 + UnityVolumeRendering + atlas MRI IXI025).

NeuroPath convierte el volumen MRI en una **herramienta de planificación quirúrgica**, no en un simple visor: además de explorar la anatomía (capas, cortes, ventana/nivel), **planificás una trayectoria de abordaje** — punto de entrada, blanco, profundidad — y el sistema te responde la pregunta clínica que importa: **¿esta trayectoria es segura, o cruza un vaso?** Todo con una UI flotante estilo XR operable desde el mouse, y con métricas en **milímetros reales**.

![La MRI volumétrica y las 4 estructuras segmentadas, alineadas en el mismo espacio](docs/images/mri_plus_meshes.png)

> 🎥 **Demo:** _(recomendado: grabá un GIF corto mostrando el corredor de seguridad cambiando de verde a rojo, la craniotomía y el MPR, guardalo en `docs/images/` y enlazalo acá.)_

---

## Índice

1. [Qué construí](#1-qué-construí)
2. [Cómo ejecutarlo](#2-cómo-ejecutarlo)
3. [Controles y uso de cada herramienta](#3-controles-y-uso-de-cada-herramienta)
4. [Arquitectura y principales decisiones técnicas](#4-arquitectura-y-principales-decisiones-técnicas)
5. [Estructura del código](#5-estructura-del-código)
6. [Limitaciones conocidas](#6-limitaciones-conocidas)
7. [Qué mejoraría con más tiempo](#7-qué-mejoraría-con-más-tiempo)
8. [Dataset incluido](#8-dataset-incluido)
9. [Troubleshooting](#9-troubleshooting)
10. [Licencia y créditos](#10-licencia-y-créditos)

---

## 1. Qué construí

Partí de la escena base (MRI volumétrica renderizada con UnityVolumeRendering + 4 mallas de segmentación: piel, sustancia gris, sustancia blanca, venas) y construí **NeuroPath**, un explorador + planificador. Las funcionalidades están agrupadas por **para qué sirven**, no por script:

### 🧠 Ver — entender la anatomía

- **Control de estructuras** (`AtlasLayers` + `LayerPanel`): opacidad por capa, mostrar/ocultar, **aislar** (fade de todo menos una estructura), "show all", y **editor de color RGB** por estructura. Las mallas vienen todas blancas; NeuroPath les da colores por defecto legibles (venas rojo, gris azulado, blanca crema, piel tono piel) para que la anatomía se lea de entrada.
- **Controles del volumen MRI** (`VolumeController` + `MriPanel`): visibilidad, modo de render **DVR / MIP / Isosurface**, y **window/level** (ventana de visibilidad min–max) para esconder la piel/aire y ver el interior.

### ✂️ Cortar — mirar adentro

- **Corte / cross-section** (`CrossSectionController`): un **plano de clipping deslizable** (Axial / Coronal / Sagital + posición) que corta **el volumen y las mallas juntos**, revelando el interior en el abordaje.
- **Craniotomía** (`CraniotomyController` + `CraniotomyPanel`): una **ventana** que se abre en la cabeza para exponer las estructuras debajo — la apertura del flap óseo, en miniatura. Redonda (por defecto, como una craniotomía real) o caja, posicionable en los 3 ejes anatómicos (L–R / S–I / A–P) y con tamaño en mm. Carva **volumen + mallas** con la misma forma.
- **MPR / cortes 2D** (`MprController` + `MprPanel`): los **tres cortes radiológicos** (axial / coronal / sagital) en escala de grises, extraídos del volumen, con un **crosshair sincronizado** entre los tres cortes **y** un marcador 3D en la escena — clickeás en un corte 2D y ves el punto moverse en el 3D. Es la comparación "volumétrico vs planos 2D" que sugiere el brief.

### 🎯 Planificar — la parte clínica

- **Planificador de trayectoria** (`TrajectoryPlanner` + `ReadoutPanel`): colocás **blanco** (superficie + profundidad ajustable hacia adentro) y **entrada**; se dibuja la línea, cada extremo es arrastrable, y el readout muestra **longitud de inserción, ángulo de abordaje** (respecto de la normal de la superficie) y **coordenadas en mm**.
- ⭐ **Corredor de seguridad** (`SafetyCorridor`): un **cilindro de radio N mm** alrededor de la trayectoria, testeado en tiempo real contra la malla de venas → **CLEAR (verde) / WARNING (rojo)**, con slider de radio. Es lo que le da propósito real al objeto "venas" y lo que convierte NeuroPath de visor en herramienta de decisión. _(Esta es la feature central; el resto la rodea.)_

### 🕹️ Interacción — cómo se toca todo

- **UI world-space estilo XR**: paneles flotantes anclados a la cámara, operados por **raycast desde el mouse** + una retícula sobre la superficie de la anatomía. Toda la UI (`UIFactory`, `UITheme`, los paneles) se construye en código.
- **Cámara** (`CameraOrbit`): órbita / zoom / pan, consciente de la UI (no orbita cuando arrastrás sobre un panel) y del modo (ver §4).

Los **5 botones del panel TOOLS funcionan** (Explore, Slices, Plan trajectory, Measure, Craniotomy) — no hay placeholders muertos. Se cubren las cuatro "ideas posibles" del brief: explorador de cortes 2D, interfaz XR con raycast, modo de inspección con clipping/crop, y comparación volumétrico↔2D.

---

## 2. Cómo ejecutarlo

- **Versión de Unity:** **`6000.4.0f1`** (Unity 6), exacta. Unity Hub → *Add* → seleccioná la carpeta del repo.
- **Render pipeline:** **Built-in** (sin setup extra; no es URP ni HDRP).
- **Escena principal:** [`Assets/NavianChallenge/Scenes/NavianChallenge_Main.unity`](Assets/NavianChallenge/Scenes/NavianChallenge_Main.unity).

**Pasos:**

1. Cloná el repo y agregalo como proyecto en Unity Hub.
2. Abrilo (la primera importación puede tardar unos minutos).
3. Abrí la escena principal y dale **Play**.

**Qué pasa al darle Play:** un único componente `NeuroPathBootstrap` presente en la escena **ensambla toda la app en runtime** — encuentra el atlas, arma la cámara, la UI world-space, el planner y el resto. Vas a ver la cabeza con las estructuras coloreadas, los paneles flotantes (MRI + estructuras a la izquierda, TOOLS + craniotomía a la derecha) y el readout de trayectoria abajo. Hay un pequeño hitch de ~1–2 s la primera vez mientras se arma la textura 3D de la MRI en la GPU (normal).

> **Nota:** el desafío se evalúa desde **Desktop / Unity Editor**. No hace falta Meta Quest ni ningún headset.

---

## 3. Controles y uso de cada herramienta

### Cámara (siempre disponible)

| Acción | Función |
|---|---|
| **Arrastrar (botón derecho)** | Orbitar — funciona en cualquier herramienta |
| **Arrastrar (botón izquierdo)** | Orbitar, **solo en modo Explore** (en las otras tools el izquierdo es de la herramienta) |
| **Rueda del mouse** | Zoom |
| **Arrastrar (botón medio)** | Pan |
| **`R`** | Resetear la cámara |

### Herramientas (panel TOOLS, derecha)

- **Explore** — solo inspección: orbitás/zoom/pan libremente.
- **Slices** — aparece la franja inferior con los 3 cortes 2D; **clickeá o arrastrá** sobre cualquier corte para mover el crosshair (los otros dos cortes y el marcador 3D se actualizan). El window/level del panel MRI re-windowea los cortes en vivo.
- **Plan trajectory** — **click 1** coloca el BLANCO (sobre la superficie, empujado a la profundidad del slider), **click 2** coloca la ENTRADA. Arrastrá los handles para reajustar; usá los sliders de **profundidad** y **radio del corredor**. El readout muestra longitud/ángulo/coords y el veredicto de seguridad.
- **Measure** — **click A**, **click B** sobre la anatomía → distancia en mm en una etiqueta flotante. Arrastrá los extremos; un click más reinicia la medición.
- **Craniotomy** — abre la ventana en la cabeza. Toggle **Round / Box**, sliders **L–R / S–I / A–P** (posición) y **Size** (mm). Salir de la herramienta la cierra.

### Panel MRI + estructuras (izquierda)

- **MRI:** visible on/off · modo DVR/MIP/Iso · Window min/max · **SECTION** (corte plano on/off, eje Axial/Coronal/Sagital, posición).
- **Estructuras:** opacidad por capa · visible · isolate · color · show all.

---

## 4. Arquitectura y principales decisiones técnicas

Esta es la parte que más muestra criterio, así que explico **el porqué** de cada decisión, no solo el qué.

### 4.1 Todo se ensambla en runtime desde un solo componente

`NeuroPathBootstrap` (un `MonoBehaviour` en la escena) arma la app entera en `Start()`: crea `AppState`, la cámara, la UI, el planner, el corredor, la craniotomía, el MPR… y **auto-encuentra** los objetos de la escena por nombre.

**Por qué:** la escena base **genera el volumen MRI en runtime** (no se serializa en el `.unity`, para mantener el repo liviano). Armar la app por código en vez de cablearla en el inspector (a) no pelea con ese volumen que aparece tarde, (b) mantiene el **diff sobre la escena base al mínimo** (un solo componente agregado, nada más serializado), y (c) hace que **toda la app sea reconstruible e inspeccionable leyendo el código**, sin cazar referencias perdidas en el editor.

### 4.2 `AppState` como fuente única del modo (la "regla de oro")

Un enum `Tool` (`Explore, Slices, PlanTrajectory, Measure, Craniotomy`) vive en un único `AppState`, con un evento `ToolChanged`. Cada sistema **lee** el modo de ahí; nadie más lo guarda.

**Por qué:** sin esto, la cámara, el planner, el measure y la craniotomía competirían por el mismo click izquierdo y se pisarían. Con un solo lugar que tiene el modo, el input es **inequívoco**: el planner solo actúa en modo `PlanTrajectory`, el measure solo en `Measure`, etc. Es la base que hace que agregar herramientas nuevas no rompa las viejas.

### 4.3 1 unidad de Unity = 1 mm

La escena mapea el FOV físico de la MRI (**240 × 240 × 180 mm** sobre 256 × 256 × 150 voxels) a unidades de mundo 1:1.

**Por qué:** las métricas clínicas tienen que ser **reales**, no arbitrarias. Gracias a esto, la longitud de inserción, el ángulo, las coordenadas del readout, el radio del corredor y el tamaño de la ventana de craniotomía están todos en **milímetros reales** — que es lo que un cirujano necesita leer.

### 4.4 UI world-space + raycast (no uGUI screen-space)

Los paneles son canvases **world-space** anclados a la cámara a un offset fijo, y se operan por **raycast desde el mouse**, con una retícula sobre la superficie.

**Por qué:** replica la interacción de **"paneles flotantes XR"** que el propio brief muestra en su mockup, pero operable 100% desde desktop. Al estar anclados a la cámara quedan siempre legibles y alcanzables sin importar cómo orbites. Se siente como un **navegador quirúrgico**, no como un HUD 2D pegado a la pantalla — que es exactamente el tono del producto de Navian.

### 4.5 Shader propio `Navian/ClippedSurface` para las mallas

Las mallas base eran `Standard` opacas. Las cambié a un shader propio, alpha-blended y de doble cara, que **corta por plano, caja y esfera** vía globals de shader.

**Por qué:** necesitaba dos cosas que el Standard no da: (a) **opacidad por estructura** para ver el interior, y (b) que las mallas se corten con **exactamente el mismo plano/caja/esfera que el volumen**. El shader lee las mismas globals de corte y **replica la matemática de cutout de UnityVolumeRendering** (misma matriz world→shape-local, mismo test de "dentro = descartar"). Resultado: el volumen y las superficies se cortan **como una sola cosa, sin desalineación** — el corte plano y la craniotomía carvan ambos a la vez. Un corte que dejara las mallas opacas tapando el interior no serviría.

### 4.6 Corredor de seguridad por muestreo de esferas

El test entry→target camina el segmento en **esferas solapadas** (`Physics.OverlapSphere`, paso = radio/2) y pregunta si cada una toca el collider de venas.

**Por qué:** la malla de venas es **no-convexa**, así que `Physics.ClosestPoint` o un capsule cast no funcionan directo contra ella. El muestreo de esferas **sí** funciona contra mesh colliders no-convexos, es simple y robusto, y a paso radio/2 es suficientemente preciso. Es un **trade-off consciente** (documentado abajo): da un veredicto booleano, no una distancia de clearance exacta, e ignora el grosor real del vaso y el margen clínico.

### 4.7 MPR extraído en CPU desde el `VolumeDataset` crudo

Los 3 cortes 2D se extraen de la data cruda del dataset (`float[]` + dims) a `Texture2D` en escala de grises, aplicando el mismo window/level que el volumen 3D.

**Por qué:** (a) da cortes **grises "radiológicos" reales** (los valores crudos, antes de la transfer function de color), que es como se leen los cortes en clínica; (b) es **autocontenido** — no necesita RenderTextures ni shaders de blit extra; (c) **comparte el window/level con el 3D** (por polling), así el 2D y el volumétrico muestran la misma ventana. La re-extracción es **lazy** (un solo plano por eje que cambia) para que el arrastre del crosshair sea fluido. El marcador 3D usa el **mismo frame del container** que la craniotomía, así el crosshair 2D y el punto 3D son literalmente el mismo voxel.

### 4.8 La cámara cede el botón izquierdo fuera de Explore

`CameraOrbit` orbita con el derecho siempre, pero con el izquierdo **solo en Explore**.

**Por qué:** las herramientas de colocación (planner, measure) necesitan el click izquierdo para poner y arrastrar puntos **sin que la cámara orbite al mismo tiempo**. Con esta regla, en Explore el izquierdo orbita, y en cualquier otra tool el izquierdo es de la herramienta mientras el derecho sigue orbitando — nunca hay dos cosas peleándose por el mismo gesto. (El controller base `AtlasSceneController` quedó **deshabilitado** por la misma razón: no puede haber dos scripts manejando la cámara.)

### 4.9 Colores por defecto legibles

Las mallas se registran con colores por defecto (venas rojo, etc.) en vez del blanco base.

**Por qué:** las 4 estructuras vienen blancas e indistinguibles; darles color de entrada hace la anatomía legible sin que el usuario tenga que configurar nada, y el editor RGB sigue estando para ajustar.

---

## 5. Estructura del código

Todo lo propio vive en `Assets/NavianChallenge/`, organizado por responsabilidad:

```
Assets/NavianChallenge/
├── Scenes/NavianChallenge_Main.unity   ← escena principal
├── Shaders/ClippedSurface.shader        ← corte de mallas (plano + caja + esfera)
└── Scripts/
    ├── Core/
    │   ├── AppState.cs                   ← fuente única del modo (enum Tool)
    │   └── NeuroPathBootstrap.cs         ← ensambla toda la app en runtime
    ├── Interaction/
    │   ├── CameraOrbit.cs                ← órbita/zoom/pan, consciente de UI y modo
    │   └── PointerRaycaster.cs           ← retícula + picking desde el mouse
    ├── UI/                               ← UI world-space, construida en código
    │   ├── UIFactory.cs · UITheme.cs     ← helpers + estilo compartido
    │   ├── MriPanel.cs · LayerPanel.cs
    │   ├── ToolPanel.cs · CraniotomyPanel.cs
    │   ├── ReadoutPanel.cs               ← readout del planner
    │   ├── MprPanel.cs · SliceView.cs    ← MPR (cortes 2D + picking)
    ├── Atlas/
    │   ├── VolumeController.cs           ← wrapper de UVR (render mode, window)
    │   ├── AtlasLayers.cs                ← opacidad/color/isolate de estructuras
    │   ├── CrossSectionController.cs     ← plano de corte (volumen + mallas)
    │   ├── CraniotomyController.cs       ← cutout box/esfera
    │   └── MprController.cs              ← extracción de cortes 2D + crosshair 3D
    └── Planning/
        ├── TrajectoryPlanner.cs          ← blanco/entrada, métricas
        ├── SafetyCorridor.cs             ← test cilindro vs venas
        └── MeasureTool.cs                ← regla de dos puntos
```

Las librerías de terceros (UnityVolumeRendering, Nifti.NET, openDicom) viven en `Assets/ThirdParty/` y **no se tocaron** salvo silenciar un par de logs informativos.

---

## 6. Limitaciones conocidas

Soy explícito con lo que **no** está resuelto — es parte del criterio:

- **Error de consola heredado de la base:** al cargar la escena aparece un error rojo *"Cannot destroy GameObject… please use Destroy instead"* que viene del `AtlasVolumeLoader` de la escena base. **No lo toqué** (es de la infraestructura base, no de NeuroPath) y no afecta el funcionamiento; lo dejé a la vista antes que enmascarar algo que no escribí.
- **Corredor de seguridad — booleano, no clearance en mm:** da CLEAR/WARNING, no la distancia exacta al vaso más cercano. Ignora el grosor real de la vena y no aplica margen clínico. El muestreo es de esferas, no una cápsula barrida exacta.
- **Sorting de transparencias:** con varias mallas alpha-blended superpuestas y sin depth-write, puede haber artefactos leves de ordenamiento en superficies cóncavas. Aceptable para un explorador; un pase de depth-peeling / OIT lo sacaría.
- **MPR — orientación cruda:** los cortes 2D se extraen en el orden crudo del dataset, no forzados a convención radiológica (nariz arriba, paciente-izquierda a la derecha). La navegación es 100% consistente (crosshair ↔ marcador ↔ cortes), pero un corte puede verse rotado/espejado respecto de la convención. Además el crosshair muestrea el voxel más cercano (sin interpolación).
- **MPR — solapamiento de UI:** la franja de cortes puede tapar un poco la esquina inferior-izquierda del panel de capas, y el window/level de los cortes se controla desde el panel MRI.
- **Blanco sin lesión segmentada:** el atlas no trae un tumor/lesión al cual "snapear", así que el blanco es un pick sobre la superficie empujado a una profundidad ajustable, no un centroide de lesión.
- **Una sola trayectoria por vez**, sin comparación ni scoring entre varias.
- **La UI se reconstruye en runtime** y no intenta sobrevivir a un *domain reload* en medio del Play en el editor.

---

## 7. Qué mejoraría con más tiempo

- **MPR completo:** orientación radiológica correcta, **proyectar la trayectoria sobre los 3 cortes** (ver dónde pasa la aguja en cada plano), scroll para barrer cortes, y muestreo interpolado.
- **Corredor mejorado:** **clearance real en mm** (distancia al vaso más cercano) + margen clínico configurable, línea de trayectoria pintada verde/roja según seguridad, y tapa sólida (capping) en los cortes.
- **Múltiples trayectorias comparables** con un score simple (longitud vs clearance vs ángulo) para elegir la mejor.
- **Robustez / pulido:** que la UI sobreviva domain reloads, re-ubicación de paneles al cambiar el aspect ratio, readout más compacto, presets de color rápidos además del RGB.
- **Higiene del repo:** agregar un `.gitignore` de Unity (excluir `Library/`, `Temp/`, `Logs/`, `UserSettings/`) para una entrega más limpia.
- **Frontera (dirección de producto):** DTI / tractografía para evitar tractos elocuentes, fMRI de áreas funcionales, **registro paciente↔imagen**, fusión CT+MRI, y llevar la misma UI world-space a **XR real en Quest** (la interacción ya está pensada para eso).

---

## 8. Dataset incluido

- **MRI:** `IXI025` T1 — 256 × 256 × 150 voxels, FOV 240 × 240 × 180 mm.
- **Estructuras (4 mallas):** `1` sustancia gris · `2` sustancia blanca · `3` venas · `4` piel.

Los datos viven dentro del proyecto (`Assets/NavianChallenge/Data/Atlas/IXI025/`), así que el repo es **autocontenido**.

---

## 9. Troubleshooting

- **«Recovering Scene Backups» al abrir:** es la auto-recuperación de Unity por un cierre previo no limpio. Podés darle **No** (la escena del repo es la buena).
- **Hitch de ~1–2 s al abrir la escena o recompilar:** es la generación de la textura 3D de la MRI. Normal.
- **No veo la MRI en modo edición:** seleccioná `AtlasRig` → `AtlasVolumeLoader` → click derecho → **Rebuild Volume**.
- **Consola limpia:** `Assets/csc.rsp` silencia warnings de *API deprecada* de la librería UnityVolumeRendering (terceros); borralo si querés verlos en tu propio código.
- **Aviso de Input Manager:** la escena usa el Input Manager clásico por simplicidad. Unity sugiere migrar al *Input System package* — es opcional.
- **Los cortes MPR se ven blancos/planos:** movés **Window min/max** en el panel MRI — la ventana por defecto puede estar muy abierta.
- **La craniotomía no abre donde esperás:** los sliders **L–R / S–I / A–P** la reposicionan; el default asume "arriba" para abrir la bóveda.

---

## 10. Licencia y créditos

- **Código propio del desafío** (escena, `Assets/NavianChallenge/`, esta documentación): **MIT** © Navian — ver [`LICENSE`](LICENSE).
- **Librerías de terceros** (mantienen su propia licencia): UnityVolumeRendering (MIT), Nifti.NET (MIT), openDicom (LGPL). Detalle en [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
- **Dataset:** la MRI IXI025 y los meshes derivados de ella están bajo **CC BY-SA 3.0**, con crédito al proyecto [IXI](https://brain-development.org/ixi-dataset/). Si redistribuís la data o trabajos derivados, mantené la atribución y la licencia.
