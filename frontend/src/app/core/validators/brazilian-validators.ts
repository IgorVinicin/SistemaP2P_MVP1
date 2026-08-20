import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export class BrazilianValidators {
  static cpf(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (!val) return null;

      const clean = val.replace(/\D/g, '');
      if (clean.length !== 11) return { cpfInvalido: true };

      // Dígitos iguais
      if (/^(\d)\1{10}$/.test(clean)) return { cpfInvalido: true };

      let sum = 0;
      for (let i = 0; i < 9; i++) {
        sum += parseInt(clean.charAt(i), 10) * (10 - i);
      }
      let rem = sum % 11;
      let digit1 = rem < 2 ? 0 : 11 - rem;
      if (parseInt(clean.charAt(9), 10) !== digit1) return { cpfInvalido: true };

      sum = 0;
      for (let i = 0; i < 10; i++) {
        sum += parseInt(clean.charAt(i), 10) * (11 - i);
      }
      rem = sum % 11;
      let digit2 = rem < 2 ? 0 : 11 - rem;
      if (parseInt(clean.charAt(10), 10) !== digit2) return { cpfInvalido: true };

      return null;
    };
  }

  static cnpj(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (!val) return null;

      const clean = val.replace(/\D/g, '');
      if (clean.length !== 14) return { cnpjInvalido: true };

      if (/^(\d)\1{13}$/.test(clean)) return { cnpjInvalido: true };

      const mult1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
      let sum = 0;
      for (let i = 0; i < 12; i++) {
        sum += parseInt(clean.charAt(i), 10) * mult1[i];
      }
      let rem = sum % 11;
      let digit1 = rem < 2 ? 0 : 11 - rem;
      if (parseInt(clean.charAt(12), 10) !== digit1) return { cnpjInvalido: true };

      const mult2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
      sum = 0;
      for (let i = 0; i < 13; i++) {
        sum += parseInt(clean.charAt(i), 10) * mult2[i];
      }
      rem = sum % 11;
      let digit2 = rem < 2 ? 0 : 11 - rem;
      if (parseInt(clean.charAt(13), 10) !== digit2) return { cnpjInvalido: true };

      return null;
    };
  }

  static maioridade(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (!val) return null;

      const birth = new Date(val);
      if (isNaN(birth.getTime())) return null;

      const today = new Date();
      let age = today.getFullYear() - birth.getFullYear();
      const monthDiff = today.getMonth() - birth.getMonth();
      if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
        age--;
      }

      return age >= 18 ? null : { menorDeIdade: true };
    };
  }
}
