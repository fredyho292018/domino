# APP-SHELL-01 STANDARD BACK ICON

The mock's common subpage header now uses an original 24×24 SVG arrow with rounded caps/joins and a 2-unit stroke matching the toolbar. The UI applies #FBFAFA tint. No bitmap assets, circles, or visible Back/Volver button text are used. The tooltip retains the accessible purpose “Back”.

The header reserves a 48×48 button on the left and an equal spacer on the right so the page title remains centered. Titles wrap within the middle column. Duplicate explicit Back actions at the foot of Plans and placeholder pages were removed; both use the shared header and the same existing State.Back navigation. Context-specific Cancel/Skip/account choices remain unchanged.

Changed: MockShellView.cs, icon_arrow_left.svg and import metadata, MockShellPreviewValidation.cs. SVG imports as UI Toolkit VectorImage. No production UI or backend changes.

Validation completed: 84 existing navigation checks and 13 toolbar navigation checks passed; standalone Unity-assembly compile passed; all 102 protected files match their inventory hashes. Unity import and the added subpage icon checks are pending the final Editor refresh. Do not interpret these as visual approval.

COMMIT=NONE
PUSH=NONE
DEPLOY=NO
