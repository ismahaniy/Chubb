import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Whitespace-only text is empty to the API ([Required] rejects it), so it is empty here too. */
export function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() === '' ? { required: true } : null;
}
