# Merge Queue Test Cases

Use these checks after changing Merge Queue behavior.

## Reorder Files

- Add three files, then drag file 3 above file 1.
- Select file 1 and file 2 with `Ctrl`, then drag both below file 3.
- Confirm file order matches the visual queue after `Merge PDF`.

## Reorder Pages In One File

- Add one multi-page file.
- Select page `02`, drag it after page `05`, then merge.
- Confirm output page order matches the new thumbnail order.

## Insert Pages Across Files

- Add file A and file B.
- Select one page from file B and drop it between page `03` and `04` of file A.
- Merge and confirm the inserted page lands in the correct position.

## Insert Whole File Into Another File

- Add file A and file B.
- Drag file B onto the page strip of file A between page `03` and `04`.
- Merge and confirm all pages of file B are inserted in order.

## Rotate Selected Pages

- Select one page, rotate right, merge, and confirm only that page rotates.
- Select multiple pages across different files, rotate left, merge, and confirm all selected pages rotate.

## Undo

- Reorder files, press `Ctrl+Z`, and confirm queue order restores.
- Insert pages between files, click `Undo`, and confirm both source and target files restore.
- Rotate selected pages, click `Undo`, and confirm rotation restores.

## Validation

- Keep one source file open in another viewer and confirm merge is blocked with a clear message.
- Add an encrypted file without password and confirm merge is blocked until password is provided.
- Confirm validation does not wipe the current page arrangement while showing warnings.
