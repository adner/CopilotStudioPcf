class SudokuGame {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.board = this.generateSolvedBoard();
        this.puzzle = this.createPuzzle(this.board);
        this.solution = JSON.parse(JSON.stringify(this.board));
        this.userBoard = JSON.parse(JSON.stringify(this.puzzle));
        this.selectedCell = null;
        this.init();
    }

    generateSolvedBoard() {
        const board = Array(9).fill().map(() => Array(9).fill(0));
        this.solveSudoku(board);
        return board;
    }

    solveSudoku(board) {
        for (let row = 0; row < 9; row++) {
            for (let col = 0; col < 9; col++) {
                if (board[row][col] === 0) {
                    const numbers = this.shuffleArray([1, 2, 3, 4, 5, 6, 7, 8, 9]);
                    for (let num of numbers) {
                        if (this.isValidMove(board, row, col, num)) {
                            board[row][col] = num;
                            if (this.solveSudoku(board)) {
                                return true;
                            }
                            board[row][col] = 0;
                        }
                    }
                    return false;
                }
            }
        }
        return true;
    }

    shuffleArray(array) {
        const shuffled = [...array];
        for (let i = shuffled.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
        }
        return shuffled;
    }

    isValidMove(board, row, col, num) {
        // Check row
        for (let x = 0; x < 9; x++) {
            if (board[row][x] === num) return false;
        }

        // Check column
        for (let x = 0; x < 9; x++) {
            if (board[x][col] === num) return false;
        }

        // Check 3x3 box
        const startRow = row - row % 3;
        const startCol = col - col % 3;
        for (let i = 0; i < 3; i++) {
            for (let j = 0; j < 3; j++) {
                if (board[i + startRow][j + startCol] === num) return false;
            }
        }

        return true;
    }

    createPuzzle(solvedBoard) {
        const puzzle = JSON.parse(JSON.stringify(solvedBoard));
        const cellsToRemove = 40 + Math.floor(Math.random() * 20); // Remove 40-60 cells for varying difficulty
        
        let removed = 0;
        while (removed < cellsToRemove) {
            const row = Math.floor(Math.random() * 9);
            const col = Math.floor(Math.random() * 9);
            
            if (puzzle[row][col] !== 0) {
                puzzle[row][col] = 0;
                removed++;
            }
        }
        
        return puzzle;
    }

    init() {
        this.createBoard();
        this.createControls();
    }

    createBoard() {
        const boardContainer = document.createElement('div');
        boardContainer.className = 'sudoku-board';
        
        for (let row = 0; row < 9; row++) {
            for (let col = 0; col < 9; col++) {
                const cell = document.createElement('div');
                cell.className = 'sudoku-cell';
                cell.dataset.row = row;
                cell.dataset.col = col;
                
                // Add thick borders for 3x3 boxes
                if (row % 3 === 0) cell.classList.add('top-border');
                if (col % 3 === 0) cell.classList.add('left-border');
                if (row === 8) cell.classList.add('bottom-border');
                if (col === 8) cell.classList.add('right-border');
                
                const value = this.puzzle[row][col];
                if (value !== 0) {
                    cell.textContent = value;
                    cell.classList.add('given');
                } else {
                    cell.addEventListener('click', () => this.selectCell(row, col));
                }
                
                boardContainer.appendChild(cell);
            }
        }
        
        this.container.appendChild(boardContainer);
    }

    createControls() {
        const controlsContainer = document.createElement('div');
        controlsContainer.className = 'sudoku-controls';
        
        // Number buttons
        const numbersContainer = document.createElement('div');
        numbersContainer.className = 'number-buttons';
        
        for (let i = 1; i <= 9; i++) {
            const button = document.createElement('button');
            button.textContent = i;
            button.className = 'number-btn';
            button.addEventListener('click', () => this.inputNumber(i));
            numbersContainer.appendChild(button);
        }
        
        // Clear button
        const clearBtn = document.createElement('button');
        clearBtn.textContent = 'Clear';
        clearBtn.className = 'clear-btn';
        clearBtn.addEventListener('click', () => this.clearCell());
        numbersContainer.appendChild(clearBtn);
        
        controlsContainer.appendChild(numbersContainer);
        
        // Action buttons
        const actionsContainer = document.createElement('div');
        actionsContainer.className = 'action-buttons';
        
        const newGameBtn = document.createElement('button');
        newGameBtn.textContent = 'New Game';
        newGameBtn.className = 'action-btn';
        newGameBtn.addEventListener('click', () => this.newGame());
        
        const checkBtn = document.createElement('button');
        checkBtn.textContent = 'Check Solution';
        checkBtn.className = 'action-btn';
        checkBtn.addEventListener('click', () => this.checkSolution());
        
        actionsContainer.appendChild(newGameBtn);
        actionsContainer.appendChild(checkBtn);
        controlsContainer.appendChild(actionsContainer);
        
        this.container.appendChild(controlsContainer);
    }

    selectCell(row, col) {
        // Remove previous selection
        const prevSelected = this.container.querySelector('.selected');
        if (prevSelected) prevSelected.classList.remove('selected');
        
        // Select new cell
        const cell = this.container.querySelector(`[data-row="${row}"][data-col="${col}"]`);
        if (cell && !cell.classList.contains('given')) {
            cell.classList.add('selected');
            this.selectedCell = { row, col };
        }
    }

    inputNumber(num) {
        if (!this.selectedCell) return;
        
        const { row, col } = this.selectedCell;
        const cell = this.container.querySelector(`[data-row="${row}"][data-col="${col}"]`);
        
        if (cell && !cell.classList.contains('given')) {
            this.userBoard[row][col] = num;
            cell.textContent = num;
            cell.classList.add('user-input');
            
            // Check if this creates a conflict
            if (!this.isValidMove(this.getUserBoardWithoutCell(row, col), row, col, num)) {
                cell.classList.add('invalid');
            } else {
                cell.classList.remove('invalid');
            }
            
            // Check if puzzle is completed
            setTimeout(() => this.checkCompletion(), 100);
        }
    }

    getUserBoardWithoutCell(excludeRow, excludeCol) {
        const board = JSON.parse(JSON.stringify(this.userBoard));
        board[excludeRow][excludeCol] = 0;
        return board;
    }

    clearCell() {
        if (!this.selectedCell) return;
        
        const { row, col } = this.selectedCell;
        const cell = this.container.querySelector(`[data-row="${row}"][data-col="${col}"]`);
        
        if (cell && !cell.classList.contains('given')) {
            this.userBoard[row][col] = 0;
            cell.textContent = '';
            cell.classList.remove('user-input', 'invalid');
        }
    }

    newGame() {
        this.container.innerHTML = '';
        this.board = this.generateSolvedBoard();
        this.puzzle = this.createPuzzle(this.board);
        this.solution = JSON.parse(JSON.stringify(this.board));
        this.userBoard = JSON.parse(JSON.stringify(this.puzzle));
        this.selectedCell = null;
        this.init();
    }

    checkSolution() {
        let isComplete = true;
        let hasErrors = false;
        
        for (let row = 0; row < 9; row++) {
            for (let col = 0; col < 9; col++) {
                if (this.userBoard[row][col] === 0) {
                    isComplete = false;
                } else if (this.userBoard[row][col] !== this.solution[row][col]) {
                    hasErrors = true;
                    const cell = this.container.querySelector(`[data-row="${row}"][data-col="${col}"]`);
                    if (cell) cell.classList.add('invalid');
                }
            }
        }
        
        if (isComplete && !hasErrors) {
            this.showMessage('Congratulations! You solved the puzzle!', 'success');
        } else if (hasErrors) {
            this.showMessage('There are some errors in your solution.', 'error');
        } else {
            this.showMessage('Keep going! The puzzle is not complete yet.', 'info');
        }
    }

    checkCompletion() {
        for (let row = 0; row < 9; row++) {
            for (let col = 0; col < 9; col++) {
                if (this.userBoard[row][col] === 0) return;
            }
        }
        
        // All cells filled, check if correct
        let isCorrect = true;
        for (let row = 0; row < 9; row++) {
            for (let col = 0; col < 9; col++) {
                if (this.userBoard[row][col] !== this.solution[row][col]) {
                    isCorrect = false;
                    break;
                }
            }
            if (!isCorrect) break;
        }
        
        if (isCorrect) {
            this.showMessage('🎉 Congratulations! You solved the puzzle! 🎉', 'success');
        }
    }

    showMessage(text, type) {
        const existing = this.container.querySelector('.sudoku-message');
        if (existing) existing.remove();
        
        const message = document.createElement('div');
        message.className = `sudoku-message ${type}`;
        message.textContent = text;
        
        this.container.appendChild(message);
        
        setTimeout(() => {
            if (message.parentNode) {
                message.remove();
            }
        }, 3000);
    }
}

// Function to create a new Sudoku game
function createSudokuGame(containerId) {
    return new SudokuGame(containerId);
}